﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NSubstitute;
using Thycotic.AppCore;
using Thycotic.DistributedEngine.Configuration;
using Thycotic.DistributedEngine.Configuration.ToMoveToSS;
using Thycotic.DistributedEngine.InteractiveRunner.Configuration;
using Thycotic.DistributedEngine.Security;
using Thycotic.Logging;
using Thycotic.MessageQueue.Client;
using Thycotic.MessageQueue.Client.QueueClient;
using Thycotic.DistributedEngine.InteractiveRunner.ConsoleCommands;
using Thycotic.Utility.Serialization;
using Thycotic.Wcf;

namespace Thycotic.DistributedEngine.InteractiveRunner
{
    internal static class Program
    {
        private static readonly ILogWriter Log = Logging.Log.Get(typeof(Program));
        private static EngineToServerCommunicationToServerWcfServiceHost _serviceHost;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                //interactive mode (first argument is i, il or icd
                //i - interactive still going out to web service to look for configuration
                //il - interactive but using a loopback for configuration
                //icd - interactive still going out to web service to look for configuration, consumption disabled
                //ilcd - interactive but using a loopback for configuration, consumption disabled
                if (args.Any() && args.First().StartsWith("i"))
                {
                    _serviceHost = new EngineToServerCommunicationToServerWcfServiceHost("net.tcp://localhost:8881");
                    _serviceHost.Start();

                    ConfigureTraceListener();

                    var cli = new CommandLineInterface();

                    Trace.TraceInformation("Starting interactive runner...");

                    #region Start server
                    var startConsuming = !args.First().EndsWith("cd");

                    EngineService engine;

                    var loopback = args.First().StartsWith("il");

                    //loopback
                    if (loopback)
                    {

                        var engineIdentificationProvider = IoCConfigurator.CreateEngineIdentificationProvider();
                        var localKeyProvider = new LocalKeyProvider();
                        var objectSerializer = new JsonObjectSerializer();
                        var loopbackCommunicationProvider = new LoopbackEngineToServerCommunicationProvider(localKeyProvider, objectSerializer);
                        var loopbackConfigurationProvider = new RemoteConfigurationProvider(
                            engineIdentificationProvider, localKeyProvider, loopbackCommunicationProvider,
                            objectSerializer);
                        var ioCConfigurator = new IoCConfigurator(localKeyProvider, loopbackCommunicationProvider, loopbackConfigurationProvider);
                        engine = new EngineService(startConsuming, ioCConfigurator);
                    }
                    else
                    {
                        engine = new EngineService(startConsuming);
                    }

                    ConfigureMockConfiguration();

                    //every time engine IoCContainer changes reconfigure the CLI
                    engine.IoCContainerConfigured += (sender, container) => ConfigureCli(cli, container);

                    engine.Start();
                    #endregion

                    //begin the input loop but after the logo prints
                    Task.Delay(StartupMessageWriter.StartupMessageDelay.Add(TimeSpan.FromMilliseconds(500)))
                        .ContinueWith(task => cli.BeginInputLoop(string.Join(" ", args.Skip(1))));

                    #region Clean up
                    cli.Wait();

                    engine.Stop();

                    if (_serviceHost != null)
                    {
                        _serviceHost.Stop();
                    }

                    #endregion

                    Trace.TraceInformation("Interactive runner stopped");

                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
                else
                {
                    var servicesToRun = new ServiceBase[]
                {
                    new EngineService()
                };
                    ServiceBase.Run(servicesToRun);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start service", ex);
            }
        }

        private static void ConfigureTraceListener()
        {
            var consoleTracer = new ConsoleTraceListener
            {
                Name = "Thycotic.DistributedEngine.InteractiveRunner.ConsoleTracer"
            };

            Trace.Listeners.Add(consoleTracer);

            Trace.TraceInformation("Console tracer attached");
        }

        private static void ConfigureCli(CommandLineInterface cli, IContainer parentContainer)
        {
            cli.ClearCommands();

            Log.Info("Configuring CLI with latest IoC configuration");

            var bus = parentContainer.Resolve<IRequestBus>();
            var exchangeNameProvider = parentContainer.Resolve<IExchangeNameProvider>();

            var currentAssembly = Assembly.GetExecutingAssembly();

            // Create the builder with which components/services are registered.
            var builder = new ContainerBuilder();

            //HACK: Why won't AsImplementedIntefaces work?? -dkk
            builder.Register(container => bus).As<IRequestBus>().SingleInstance();
            builder.Register(container => exchangeNameProvider).As<IExchangeNameProvider>().SingleInstance();

            builder.RegisterAssemblyTypes(currentAssembly)
                .Where(t => !t.IsAbstract)
                .Where(t => typeof(IConsoleCommand).IsAssignableFrom(t))
                .Where(t => t != typeof(SystemConsoleCommand));

            var tempContainer = builder.Build();

            var commands =
                tempContainer.ComponentRegistry.Registrations.Where(
                    r => typeof(IConsoleCommand).IsAssignableFrom(r.Activator.LimitType));

            commands.ToList().ForEach(c => cli.AddCustomCommand((IConsoleCommand)tempContainer.Resolve(c.Activator.LimitType)));
        }

        private static void ConfigureMockConfiguration()
        {
            var configurationProvider =  Substitute.For<IConfigurationProvider>();
            ServiceLocator.ConfigurationProvider = configurationProvider;

            var configuration = Substitute.For<IConfiguration>();
            configuration.FipsEnabled.Returns(false);

            configurationProvider.GetCurrentConfiguration().Returns(configuration);



        }
    }
}

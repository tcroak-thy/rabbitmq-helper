﻿using System;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Thycotic.Wcf
{
    /// <summary>
    /// Base WCF server wrapper
    /// </summary>
    public class ServiceHostBase<TServer, TEndPoint> : IDisposable
    {
        private readonly string _connectionString;
        private readonly bool _useSsl;
        private readonly string _thumbprint;
        private Task _serverTask;
        private ServiceHost _host;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceHostBase{TServer,TEndPoint}"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public ServiceHostBase(string connectionString)
        {
            _connectionString = connectionString;
            _useSsl = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceHostBase{TServer,TEndPoint}" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="thumbprint">The thumbprint.</param>
        public ServiceHostBase(string connectionString, string thumbprint)
        {
            _connectionString = connectionString;
            _useSsl = true;
            _thumbprint = thumbprint;
        }

        /// <summary>
        /// Perform once-off startup processing.
        /// </summary>
        /// <exception cref="System.ApplicationException">Service already running</exception>
        public virtual void Start()
        {
            if (_serverTask != null)
            {
                throw new ApplicationException("Service already running");
            }

            _serverTask = Task.Factory.StartNew(() =>
            {
                NetTcpBinding serviceBinding;

                if (_useSsl)
                {
                    serviceBinding = new NetTcpBinding(SecurityMode.Transport);
                    serviceBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
                }
                else
                {
                    serviceBinding = new NetTcpBinding(SecurityMode.None);
                }
                serviceBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;

                _host = new ServiceHost(typeof (TServer));//MemoryMqWcfService));
                _host.AddServiceEndpoint(typeof(TEndPoint), serviceBinding, _connectionString);//typeof(IMemoryMqWcfService)

                if (_useSsl)
                {
                    _host.Credentials.ServiceCertificate.SetCertificate(
                        StoreLocation.LocalMachine,
                        StoreName.My,
                        X509FindType.FindByThumbprint,
                        _thumbprint);
                }
                
                _host.Open();
            });
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public virtual void Stop()
        {
            if ((_host == null) || (_serverTask == null)) return;

            if (_host.State == CommunicationState.Opened)
            {
                _host.Close();
            }
            _serverTask.Wait();
                
            _host = null;
            _serverTask = null;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }

}
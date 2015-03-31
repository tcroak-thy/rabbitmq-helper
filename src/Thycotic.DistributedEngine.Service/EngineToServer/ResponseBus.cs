using System;
using Thycotic.DistributedEngine.EngineToServerCommunication;
using Thycotic.DistributedEngine.EngineToServerCommunication.Areas.Heartbeat.Response;
using Thycotic.DistributedEngine.EngineToServerCommunication.Areas.PasswordChanging.Response;
using Thycotic.DistributedEngine.EngineToServerCommunication.Engine.Envelopes;
using Thycotic.DistributedEngine.EngineToServerCommunication.Engine.Request;
using Thycotic.DistributedEngine.EngineToServerCommunication.Engine.Response;
using Thycotic.DistributedEngine.Logic.EngineToServer;
using Thycotic.DistributedEngine.Service.Security;
using Thycotic.Encryption;
using Thycotic.Utility.Serialization;

namespace Thycotic.DistributedEngine.Service.EngineToServer
{
    /// <summary>
    /// Engine to server communication provider
    /// </summary>
    public class ResponseBus : IResponseBus
    {
        private readonly IObjectSerializer _objectSerializer;
        private readonly IAuthenticatedCommunicationKeyProvider _authenticatedCommunicationKeyProvider;
        private readonly IAuthenticatedCommunicationRequestEncryptor _authenticatedCommunicationRequestEncryptor;
        private readonly IEngineToServerCommunicationWcfService _channel;

        /// <summary>
        /// Initializes a new instance of the <see cref="EngineConfigurationBus" /> class.
        /// </summary>
        /// <param name="engineToServerConnection">The engine to server connection.</param>
        /// <param name="objectSerializer">The object serializer.</param>
        /// <param name="authenticatedCommunicationKeyProvider">The authenticated communication key provider.</param>
        /// <param name="authenticatedCommunicationRequestEncryptor">The authenticated communication request encryptor.</param>
        public ResponseBus(IEngineToServerConnection engineToServerConnection,
            IObjectSerializer objectSerializer,
            IAuthenticatedCommunicationKeyProvider authenticatedCommunicationKeyProvider,
            IAuthenticatedCommunicationRequestEncryptor authenticatedCommunicationRequestEncryptor)
        {
            _objectSerializer = objectSerializer;
            _authenticatedCommunicationKeyProvider = authenticatedCommunicationKeyProvider;
            _authenticatedCommunicationRequestEncryptor = authenticatedCommunicationRequestEncryptor;
            _channel = engineToServerConnection.OpenChannel();
        }

        private SymmetricEnvelope WrapRequest(object response)
        {
            var requestString = _objectSerializer.ToBytes(response);

            return new SymmetricEnvelope
            {
                KeyHash = _authenticatedCommunicationKeyProvider.SymmetricKey.GetHashString(),
                Body = _authenticatedCommunicationRequestEncryptor.Encrypt((SymmetricKeyPair)_authenticatedCommunicationKeyProvider, requestString)
            };
        }

        private T UnwrapRequest<T>(byte[] bytes)
        {
            var unencryptedBytes = _authenticatedCommunicationRequestEncryptor.Decrypt((SymmetricKeyPair) _authenticatedCommunicationKeyProvider, bytes);

            return _objectSerializer.ToObject<T>(unencryptedBytes);
        }

        /// <summary>
        /// Wraps the channel interaction
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public T WrapInteraction<T>(Func<T> func)
        {
            try
            {
                return func.Invoke();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Bus broken down", ex);
            }
        }

        /// <summary>
        /// Wraps the channel interaction
        /// </summary>
        /// <param name="action"></param>
        public void WrapInteraction(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Bus broken down", ex);
            }
        }

        /// <summary>
        /// Sends a heartbeat request to server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        public EngineHeartbeatResponse SendHeartbeat(EngineHeartbeatRequest request)
        {
            var response = WrapInteraction(() => _channel.SendHeartbeat(WrapRequest(request)));

            return UnwrapRequest<EngineHeartbeatResponse>(response);
        }

        /// <summary>
        /// Pings the specified envelope.
        /// </summary>
        /// <param name="request">The request.</param>
        public void Ping(EnginePingRequest request)
        {
           WrapInteraction(() => _channel.Ping(WrapRequest(request)));   
        }

        /// <summary>
        /// Sends the secret heartbeat response.
        /// </summary>
        /// <param name="response">The response.</param>
        public void SendSecretHeartbeatResponse(SecretHeartbeatResponse response)
        {
            WrapInteraction(() => _channel.SendSecretHeartbeatResponse(WrapRequest(response)));
        }

        /// <summary>
        /// Sends the remote password change response.
        /// </summary>
        /// <param name="response">The response.</param>
        public void SendRemotePasswordChangeResponse(RemotePasswordChangeResponse response)
        {
           WrapInteraction(() =>  _channel.SendRemotePasswordChangeResponse(WrapRequest(response)));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            WrapInteraction(() => _channel.Dispose());
        }

    }
}
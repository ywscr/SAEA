﻿/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： StreamServerSocket
*版本号： v5.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// 服务器 socket
    /// </summary>
    public class StreamServerSocket : IServerSokcet, IDisposable
    {
        Socket _listener;

        int _clientCounts;

        private readonly CancellationToken _cancellationToken;

        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        public ISocketOption SocketOption { get; set; }

        bool _isStoped = true;

        public bool IsDisposed
        {
            get; set;
        } = false;

        #region events

        public event OnAcceptedHandler OnAccepted;
        public event OnErrorHandler OnError;
        public event OnDisconnectedHandler OnDisconnected;
        [Obsolete("此方法仅用于IOCP中")]
        public event OnReceiveHandler OnReceive;

        #endregion

        /// <summary>
        /// 服务器 socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        public StreamServerSocket(ISocketOption socketOption, CancellationToken cancellationToken)
        {
            SocketOption = socketOption;
            _cancellationToken = cancellationToken;
        }
        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start(int backlog = 10 * 1000)
        {
            if (_listener == null)
            {
                _isStoped = false;

                _listener = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.NoDelay = SocketOption.NoDelay;

                if (SocketOption.UseIPV6)
                {
                    _listener.Bind(new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                }
                else
                {
                    _listener.Bind(new IPEndPoint(IPAddress.Any, SocketOption.Port));
                }

                _listener.Listen(backlog);

                Task.Run(ProcessAccepted, _cancellationToken);
            }
        }

        private async Task ProcessAccepted()
        {
            while (!_isStoped)
            {
                Socket clientSocket = null;
                try
                {
                    if (_listener == null) break;

                    clientSocket = await _listener.AcceptAsync().ConfigureAwait(false);

                    clientSocket.NoDelay = true;

                    if (clientSocket == null || clientSocket.RemoteEndPoint == null) break;

                    Stream nsStream;

                    if (SocketOption.WithSsl)
                    {
                        nsStream = new SslStream(new NetworkStream(clientSocket), false);
                        await ((SslStream)nsStream).AuthenticateAsServerAsync(SocketOption.X509Certificate2, false, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false).ConfigureAwait(false);
                    }
                    else
                    {
                        nsStream = new NetworkStream(clientSocket);
                    }
                    var id = clientSocket.RemoteEndPoint.ToString();

                    var ci = ChannelManager.Current.Set(id, clientSocket, nsStream);

                    OnAccepted?.Invoke(ci);
                }
                catch (ObjectDisposedException oex)
                {
                    OnError?.Invoke(string.Empty, oex);
                }
                catch (AuthenticationException aex)
                {
                    OnError?.Invoke(string.Empty, aex);
                    OnDisconnected?.Invoke(SocketOption.IP + "_" + SocketOption.Port, aex);
                    clientSocket?.Close();
                }
                catch (Exception exception)
                {
                    OnError?.Invoke(string.Empty, exception);

                    if (exception is SocketException s && s.SocketErrorCode == SocketError.OperationAborted)
                    {
                        OnDisconnected?.Invoke(SocketOption.IP + "_" + SocketOption.Port, exception);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), _cancellationToken).ConfigureAwait(false);
                    clientSocket?.Close();
                }
            }
        }
        public object GetCurrentObj(string sessionID)
        {
            return ChannelManager.Current.Get(sessionID);
        }

        public void SendAsync(string sessionID, byte[] data)
        {
            var channel = ChannelManager.Current.Get(sessionID);
            channel.Stream.WriteAsync(data, 0, data.Length);
        }

        public void Send(string sessionID, byte[] data)
        {
            var channel = ChannelManager.Current.Get(sessionID);
            channel.Stream.Write(data, 0, data.Length);
        }

        public void End(string sessionID, byte[] data)
        {
            var channel = ChannelManager.Current.Get(sessionID);
            channel.Stream.Write(data, 0, data.Length);
            Disconnecte(sessionID);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID"></param>
        public void Disconnecte(string sessionID)
        {
            var channel = ChannelManager.Current.Get(sessionID);
            var socket = channel.ClientSocket;
            if (socket != null)
                socket.Close();
            ChannelManager.Current.Remove(sessionID);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Stop()
        {
            _isStoped = true;
            try
            {
                SocketOption.X509Certificate2?.Dispose();
                _listener.Close();
            }
            catch { }
            try
            {
                _listener?.Dispose();
                _listener = null;
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            IsDisposed = true;
        }

        
    }
}

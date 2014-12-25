﻿using System.Threading;

namespace ZeroMQ
{
	using System;
    using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.InteropServices;
    using lib;

    /// <summary>
    /// Sends and receives messages across various transports to potentially multiple endpoints
    /// using the ZMQ protocol.
    /// </summary>
    public class ZSocket : IDisposable
	{

		/// <summary>
		/// Create a socket with the current context and the specified socket type.
		/// </summary>
		/// <param name="socketType">A <see cref="ZSocketType"/> value for the socket.</param>
		/// <returns>A <see cref="ZSocket"/> instance with the current context and the specified socket type.</returns>
		public static ZSocket Create(ZContext context, ZSocketType socketType, out ZError error)
		{
			error = default(ZError);

			IntPtr socketPtr;
			while (IntPtr.Zero == (socketPtr = zmq.socket(context.ContextPtr, socketType.Number))) 
			{
				error = ZError.GetLastErr();
				
				if (error == ZError.EINTR) {
					error = default(ZError);
					continue;
				}
				if (error == ZError.EMFILE) {
					return default(ZSocket);
				}
				if (error == ZError.ETERM) {
					return default(ZSocket);
				}
				if (error == ZError.EFAULT) {
					return default(ZSocket);
				}

				throw new ZException (error);
			}

			return new ZSocket(socketPtr, socketType);
		}


		private IntPtr _socketPtr;

		private ZSocketType _socketType;

		internal ZSocket(IntPtr socketPtr, ZSocketType socketType)
		{
			_socketPtr = socketPtr;
			_socketType = socketType;
		}

		/// <summary>
		/// Finalizes an instance of the <see cref="ZSocket"/> class.
		/// </summary>
		~ZSocket()
		{
			Dispose(false);
		}

		private bool _disposed;

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ZSocket"/>, and optionally disposes of the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					Close();
				}
			}

			_disposed = true;
		}

		/// <summary>
		/// Close the current socket.
		/// </summary>
		/// <remarks>
		/// Any outstanding messages physically received from the network but not yet received by the application
		/// with Receive shall be discarded. The behaviour for discarding messages sent by the application
		/// with Send but not yet physically transferred to the network depends on the value of
		/// the <see cref="Linger"/> socket option.
		/// </remarks>
		/// <exception cref="ZmqSocketException">The underlying socket object is not valid.</exception>
		public void Close()
		{
			if (_socketPtr == IntPtr.Zero) return;

			while (-1 == zmq.close(SocketPtr)) {
				var error = ZError.GetLastErr();

				if (error == ZError.EINTR) {
					continue;
				}				
				if (error == ZError.ENOTSOCK) {
					// The provided socket was invalid.
					break;
				} else {
					throw new ZException (error);
				}
			}
			_socketPtr = IntPtr.Zero;
		}



		/*

        /// <summary>
        /// The maximum buffer length when using the high performance Send/Receive methods (8192).
        /// </summary>
        public const int MaxBufferSize = SocketProxy.MaxBufferSize;

        private const int LatestVersion = 3;

#pragma warning disable 618
        private static readonly SocketOption ReceiveHwmOpt = ZmqVersion.Current.IsAtLeast(LatestVersion) ? ZmqSocketOption.RCVHWM : ZmqSocketOption.HWM;
        private static readonly SocketOption SendHwmOpt = ZmqVersion.Current.IsAtLeast(LatestVersion) ? ZmqSocketOption.SNDHWM : ZmqSocketOption.HWM;
#pragma warning restore 618

        private readonly SocketProxy _socketProxy;

        private bool _disposed;

        /// <summary>
        /// Occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventPtrr<SocketEventArgs> ReceiveReady;

        /// <summary>
        /// Occurs when at least one message may be sent via the socket without blocking.
        /// </summary>
        public event EventPtrr<SocketEventArgs> SendReady;
		*/ 
		

		public IntPtr SocketPtr
		{
			get { return _socketPtr; }
		}

		/*/ <summary>
		/// Gets the status of the last Receive operation.
		/// </summary>
		public ZmqStatus ReceiveStatus { get; private set; } */

		/*/ <summary>
		/// Gets the status of the last Send operation.
		/// </summary>
		public ZmqStatus SendStatus { get; private set; } */

        /// <summary>
        /// Gets the <see cref="ZeroMQ.ZSocketType"/> value for the current socket.
        /// </summary>
		public ZSocketType SocketType { get { return _socketType; } }

        /// <summary>
        /// Create an endpoint for accepting connections and bind it to the current socket.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred binding the socket to an endpoint.</exception>
        /// <exception cref="System.ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        public bool Bind(string endpoint, out ZError error)
		{
			EnsureNotDisposed();

			bool result = false;
			error = default(ZError);

			if (string.IsNullOrWhiteSpace(endpoint)) {
				throw new ArgumentException ("IsNullOrWhiteSpace", "endpoint");
			}

			int endpointPtrSize;
			using (var endpointPtr = DispoIntPtr.AllocString(endpoint, out endpointPtrSize)) 
			{
				while (!(result = (-1 != zmq.bind(_socketPtr, endpointPtr)))) {
					error = ZError.GetLastErr();
				
					if (error == ZError.EINTR) {
						error = default(ZError);
						continue;
					}
					if (error == ZError.ETERM
					    || error == ZError.ENOTSOCK // The provided socket was invalid.
					    || error == ZError.EADDRINUSE
					    || error == ZError.EADDRNOTAVAIL
					    || error == ZError.ENODEV
					    || error == ZError.EMTHREAD 
				    ) {
						break;
					}

					// error == ZmqError.EINVAL
					throw new ZException (error);
				}
			}
			return result;
        }

        /// <summary>
        /// Stop accepting connections for a previously bound endpoint on the current socket.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred unbinding the socket to an endpoint.</exception>
        /// <exception cref="System.ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        public bool Unbind(string endpoint, out ZError error)
        {
            EnsureNotDisposed();

			bool result = false;
			error = default(ZError);

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("IsNullOrWhiteSpace", "endpoint");
            }
			
			int endpointPtrSize;
			using (var endpointPtr = DispoIntPtr.AllocString(endpoint, out endpointPtrSize)) 
			{
				while (!(result = (-1 != zmq.unbind(_socketPtr, endpointPtr)))) {
					error = ZError.GetLastErr();
				
					if (error == ZError.EINTR) {
						error = default(ZError);
						continue;
					}
					if (error == ZError.ETERM
					    || error == ZError.ENOTSOCK // The provided socket was invalid.
					    || error == ZError.ENOENT
				    ) {
						break;
					}

					// error == ZmqError.EINVAL
					throw new ZException (error);
				}
			}
			return result;
        }

        /// <summary>
        /// Connect the current socket to the specified endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred connecting the socket to a remote endpoint.</exception>
        /// <exception cref="System.ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        public bool Connect(string endpoint, out ZError error)
        {
            EnsureNotDisposed();

			bool result = false;
			error = default(ZError);

			if (string.IsNullOrWhiteSpace(endpoint))
			{
				throw new ArgumentException("IsNullOrWhiteSpace", "endpoint");
			}

			int endpointPtrSize;
			using (var endpointPtr = DispoIntPtr.AllocString(endpoint, out endpointPtrSize)) 
			{
				while (!(result = (-1 != zmq.connect(_socketPtr, endpointPtr)))) {
					error = ZError.GetLastErr();

					if (error == ZError.EINTR) {
						error = default(ZError);
						continue;
					}
					if (error == ZError.ETERM
					    || error == ZError.ENOTSOCK // The provided socket was invalid.
				    	|| error == ZError.ENOENT
					    || error == ZError.EMTHREAD 
					    ) {
						break;
					}

					// error == ZmqError.EINVAL
					throw new ZException (error);
				}
			}
			return result;
        }

        /// <summary>
        /// Disconnect the current socket from a previously connected endpoint.
        /// </summary>
        /// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
        /// <exception cref="ZmqSocketException">An error occurred disconnecting the socket from a remote endpoint.</exception>
        /// <exception cref="System.ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        public bool Disconnect(string endpoint, out ZError error)
        {
            EnsureNotDisposed();

			bool result = false;
			error = default(ZError);

			if (string.IsNullOrWhiteSpace(endpoint))
			{
				throw new ArgumentException("IsNullOrWhiteSpace", "endpoint");
			}
			
			int endpointPtrSize;
			using (var endpointPtr = DispoIntPtr.AllocString(endpoint, out endpointPtrSize)) 
			{
				while (!(result = (-1 != zmq.disconnect(_socketPtr, endpointPtr)))) {
					error = ZError.GetLastErr();
				
					if (error == ZError.EINTR) {
						error = default(ZError);
						continue;
					}
					if (error == ZError.ETERM
					    || error == ZError.ENOTSOCK // The provided socket was invalid.
				    	|| error == ZError.ENOENT
				    ) {
						break;
					}

					// EINVAL
					throw new ZException (error);
				}
			}
			return result;
        }

		public bool Receive(out byte[] buffer, out ZError error)
		{
			return Receive(1, ZSocketFlags.None, out buffer, out error);
		}

		public bool ReceiveMany(int receiveCount, ZSocketFlags flags, out byte[] buffer, out ZError error)
		{
			return Receive(receiveCount, flags | ZSocketFlags.More, out buffer, out error);
		}

		public bool ReceiveMany(int receiveCount, out byte[] buffer, out ZError error)
		{
			return Receive(receiveCount, ZSocketFlags.More, out buffer, out error);
		}

		public bool ReceiveAll(out byte[] buffer, out ZError error)
		{
			return ReceiveAll(ZSocketFlags.More, out buffer, out error);
		}

		public bool ReceiveAll(ZSocketFlags flags, out byte[] buffer, out ZError error)
		{
			bool more = (flags & ZSocketFlags.More) == ZSocketFlags.More;
			return Receive(more ? int.MaxValue : 1, flags, out buffer, out error);
		}
			
		public virtual bool Receive(int receiveCount, ZSocketFlags flags, out byte[] buffer, out ZError error)
		{
			error = default(ZError);
			bool result = false;
			EnsureNotDisposed();
			// EnsureReceiveSocket();

			buffer = null;

			List<ZFrame> frames = ReceiveFrames(receiveCount, flags, out error).ToList();
			result = ( error == default(ZError) );

			int receivedCount = frames.Count;
			if (result && receiveCount > 0) {
				// Aggregate all buffers
				int length = frames.Aggregate(0, (counter, frame) => counter + (int)frame.Length);
				buffer = new byte[length];

				// Concatenate the buffers
				int offset = 0;
				for (int i = 0, l = receivedCount; i < l; ++i) {
					ZFrame frame = frames [i];
					int len = (int)frame.Length;
					Marshal.Copy(frame.DataPtr(), buffer, offset, len);
					offset += len;

					frame.Dispose();
				}
			} else {
				for (int i = 0, l = receivedCount; i < l; ++i) {
					ZFrame frame = frames [i];
					frame.Dispose();
				}
			}

			return result;
		}

		public ZMessage ReceiveMessage(out ZError error)
		{
			return ReceiveMessage(ZSocketFlags.None, out error);
		}

		public ZMessage ReceiveMessage(ZSocketFlags flags, out ZError error)
		{
			ZMessage message = null;
			flags |= ZSocketFlags.More;
			bool result = ReceiveMessage(int.MaxValue, flags, ref message, out error);
			if (result) {
				return message;
			}
			return null;
		}

		public bool ReceiveMessage(int receiveCount, ZSocketFlags flags, ref ZMessage message, out ZError error)
		{
			error = default(ZError);
			bool result = false;
			EnsureNotDisposed();
			// EnsureReceiveSocket();

			IEnumerable<ZFrame> framesQuery = ReceiveFrames(receiveCount, flags, out error);
			result = error == default(ZError);

			if (result) {
				if (message == null) {
					message = new ZMessage (framesQuery);
				} else {
					message.AddRange(framesQuery);
				}
			}
			return result;
		}

        public ZFrame ReceiveFrame(out ZError error)
        {
            return ReceiveFrame(ZSocketFlags.None, out error);
        }

        public ZFrame ReceiveFrame(ZSocketFlags flags, out ZError error)
        {
            return ReceiveFrames(1, flags, out error).FirstOrDefault();
        }

		public IEnumerable<ZFrame> ReceiveFrames(out ZError error)
		{
			return ReceiveFrames ( int.MaxValue, ZSocketFlags.More, out error );
		}

        public IEnumerable<ZFrame> ReceiveFrames(int receiveCount, out ZError error)
        {
            return ReceiveFrames(receiveCount, ZSocketFlags.None, out error);
        }

		public IEnumerable<ZFrame> ReceiveFrames(int receiveCount, ZSocketFlags flags, out ZError error)
		{
			bool result = false;
			error = default(ZError);

			bool more = receiveCount > 1 && ((flags & ZSocketFlags.More) == ZSocketFlags.More);
			var frames = new List<ZFrame>();

			do {
				var frame = ZFrame.CreateEmpty();

				while (!(result = (-1 != zmq.msg_recv(frame.Ptr, _socketPtr, (int)flags)))) {
					error = ZError.GetLastErr();

					if (error == ZError.EINTR) {
						// if (--retry > -1)
						error = default(ZError);
						continue;
					}

					frame.Dispose();

					if (error == ZError.EAGAIN) {
						break;
					}
					if (error == ZError.ETERM) {
						break;
					}

					throw new ZException (error);
				}
				if (error == ZError.EAGAIN) {

					Thread.Yield();
					continue;
				}
				if (result) {
					frames.Add(frame);

					if (more) {
						more = ReceiveMore;
					}
				}

			} while (result && more && --receiveCount > 0);

			return frames;
		}

		public virtual bool Send(byte[] buffer, out ZError error) 
		{
			return Send(buffer, ZSocketFlags.None, out error);
		}

		public virtual bool SendMore(byte[] buffer, out ZError error) 
		{
			return Send(buffer, ZSocketFlags.More, out error);
		}

		public virtual bool Send(byte[] buffer, ZSocketFlags flags, out ZError error) 
		{
			EnsureNotDisposed();
			EnsureSendSocket();

			error = default(ZError);
			bool result = false;

			int size = buffer.Length;
			//IntPtr bufP = Marshal.AllocHGlobal(size);
			var frame = ZFrame.Create(size);
			Marshal.Copy(buffer, 0, frame.DataPtr(), size);

			result = SendFrameInternal(frame, flags, out error);

			return result;
		}

		public virtual bool SendMessage(ZMessage msg, out ZError error)
		{
			return SendMessage(msg, ZSocketFlags.None, out error);
		}

		public virtual bool SendMessageMore(ZMessage msg, out ZError error)
		{
			return SendMessage(msg, ZSocketFlags.More, out error);
		}

		public virtual bool SendMessage(ZMessage msg, ZSocketFlags flags, out ZError error)
		{
			return SendFrames(msg, flags, out error);
		}

		public virtual bool SendFrames(IEnumerable<ZFrame> frames, out ZError error)
		{
			return SendFrames(frames, ZSocketFlags.None, out error);
		}

		public virtual bool SendFrames(IEnumerable<ZFrame> frames, ZSocketFlags flags, out ZError error) 
		{
			EnsureNotDisposed();
			EnsureSendSocket();

			error = default(ZError);
			bool result = false;
			bool finallyMore = (flags & ZSocketFlags.More) == ZSocketFlags.More;

			for (int i = 0, l = frames.Count(); i < l; ++i) {
				ZSocketFlags frameFlags = flags | ZSocketFlags.More;
				if (i == l - 1 && !finallyMore) {
					frameFlags &= ~ZSocketFlags.More;
				}
				if (!(result = SendFrameInternal(frames.ElementAt(i), frameFlags, out error))) {
					break;
				}
			}

			return result;
		}

		public virtual bool SendFrame(ZFrame msg, out ZError error)
		{
			EnsureNotDisposed();
			EnsureSendSocket();

			return SendFrameInternal(msg, ZSocketFlags.None, out error);
		}

		public virtual bool SendFrameMore(ZFrame msg, out ZError error)
		{
			EnsureNotDisposed();
			EnsureSendSocket();

			return SendFrameInternal(msg, ZSocketFlags.More, out error);
		}

		public virtual bool SendFrame(ZFrame msg, ZSocketFlags flags, out ZError error)
		{
			EnsureNotDisposed();
			EnsureSendSocket();

			return SendFrameInternal(msg, flags, out error);
		}

		private bool SendFrameInternal(ZFrame frame, ZSocketFlags flags, out ZError error) 
		{
			error = default(ZError);
			bool result = false;

			using (frame) {
				while (!(result = !(-1 == zmq.msg_send(frame.Ptr, _socketPtr, (int)flags)))) {
					error = ZError.GetLastErr();

					if (error == ZError.EINTR) {
						error = default(ZError);
						continue;
					}
					if (error == ZError.EAGAIN) {
						error = default(ZError);
						// TODO: ZmqSocketFlags.AutoAgain?

						Thread.Yield();
						continue;
					}
					if (error == ZError.ETERM) {
						break;
					} 

					throw new ZException (error);
				}
				if (result) {
					// Tell IDisposable to not unallocate zmq_msg
					frame.Dismiss();
				}
			}

			return result;
		}

		// message is always null
		public bool Forward(ZSocket destination, out ZMessage message, out ZError error)
		{
			error = default(ZError);
			message = null;
			bool result = false;
			bool more = false;

			using (var msg = ZFrame.CreateEmpty()) {
				do {

					// receiving scope
					{
						// var recvErr = default(ZmqError);
						while (!(result = (-1 != zmq.msg_recv(msg.Ptr, this.SocketPtr, (int)ZSocketFlags.DontWait)))) {
							error = ZError.GetLastErr();
							
							if (error == ZError.EINTR) {
								error = null;
								continue;
							}
							if (error == ZError.EAGAIN) {
								error = null;
								// Thread.Yield();
								Thread.Sleep(0);
								continue;
							}

							break;
						}
						if (!result) {
							// error = recvErr;
							break;
						}
					}

					// will have to receive more?
					more = ReceiveMore;
					
					// sending scope
					{ 
						// var sndErr = default(ZmqError);
						while (!(result = (-1 != zmq.msg_send(msg.Ptr, destination.SocketPtr, more ? (int)(ZSocketFlags.More | ZSocketFlags.DontWait) : (int)ZSocketFlags.DontWait)))) {
							error = ZError.GetLastErr();
							
							if (error == ZError.EINTR) {
								error = null;
								continue;
							}
							if (error == ZError.EAGAIN) {
								error = null;
								// Thread.Yield();
								Thread.Sleep(0);
								continue;
							}

							break;
						}
						if (result) {
							// msg.Dismiss();
						}
						else {
							// error = sndErr;
							break;
						}
					}
					
				} while (result && more);

			} // using (msg) -> Dispose


			return result;
		}


        /*/ <summary>
        /// Queue a message buffer to be sent by the socket in blocking mode.
        /// </summary>
        /// <remarks>
        /// Performance tip: To increase send performance, especially on low-powered devices, restrict the
        /// size of <paramref name="buffer"/> to <see cref="MaxBufferSize"/>. This will reduce the number of
        /// P/Invoke calls required to send the message buffer.
        /// </remarks>
        /// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
        /// <param name="size">The size of the message to send.</param>
        /// <param name="flags">A combination of <see cref="SocketFlags"/> values to use when sending.</param>
        /// <returns>The number of bytes sent by the socket.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is a negative value or is larger than the length of <paramref name="buffer"/>.</exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public virtual int Send(byte[] buffer, int size, SocketFlags flags)
        {
            EnsureNotDisposed();

            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (size < 0 || size > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("size", "Expected a non-negative value less than or equal to the buffer length.");
            }

            int sentBytes = _socketProxy.Send(buffer, size, (int)flags);

            if (sentBytes >= 0)
            {
                SendStatus = (sentBytes == size || LibZmq.MajorVersion < LatestVersion) ? SendStatus.Sent : SendStatus.Incomplete;
                return sentBytes;
            }

            if (ErrorProxy.ShouldTryAgain)
            {
                SendStatus = SendStatus.TryAgain;
                return -1;
            }

            if (ErrorProxy.ContextWasTerminated)
            {
                SendStatus = SendStatus.Interrupted;
                return -1;
            }

            throw new ZmqSocketException(ErrorProxy.GetLastError());
        }

        /// <summary>
        /// Queue a message buffer to be sent by the socket in non-blocking mode with a specified timeout.
        /// </summary>
        /// <remarks>
        /// Performance tip: To increase send performance, especially on low-powered devices, restrict the
        /// size of <paramref name="buffer"/> to <see cref="MaxBufferSize"/>. This will reduce the number of
        /// P/Invoke calls required to send the message buffer.
        /// </remarks>
        /// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
        /// <param name="size">The size of the message to send.</param>
        /// <param name="flags">A combination of <see cref="SocketFlags"/> values to use when sending.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
        /// <returns>The number of bytes sent by the socket.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="size"/> is a negative value or is larger than the length of <paramref name="buffer"/>.
        /// </exception>
        /// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
        public int Send(byte[] buffer, int size, SocketFlags flags, TimeSpan timeout)
        {
            return timeout == TimeSpan.MaxValue
                    ? Send(buffer, size, flags & ~SocketFlags.DontWait)
                    : this.WithTimeout(Send, buffer, size, flags | SocketFlags.DontWait, timeout);
        } */

        /*/ <summary>
        /// Forwards a single-part or all parts of a multi-part message to a destination socket.
        /// </summary>
        /// <remarks>
        /// This method is useful for implementing devices as data is not marshalled into managed code; it
        /// is forwarded directly in the unmanaged layer. As an example, this method could forward all traffic
        /// from a device's front-end socket to its backend socket.
        /// </remarks>
        /// <param name="destination">A <see cref="ZmqSocket"/> that will receive the incoming message(s).</param>
        public void Forward(ZmqSocket destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }

            if (_socketProxy.Forward(destination.SocketPtr) == -1)
            {
                throw new ZmqSocketException(ErrorProxy.GetLastError());
            }
		} */

		// From options.hpp: unsigned char identity [256];
		private const int MaxBinaryOptionSize = 255;

		private bool GetOption(ZSocketOption option, IntPtr optionValue, ref int optionLength)
		{
			EnsureNotDisposed();

			bool result = false;

			using (var optionLengthP = DispoIntPtr.Alloc(IntPtr.Size)) {

				if (IntPtr.Size == 4)
					Marshal.WriteInt32(optionLengthP.Ptr, optionLength);
				else if (IntPtr.Size == 8)
					Marshal.WriteInt64(optionLengthP.Ptr, (long)optionLength);
				else 
					throw new PlatformNotSupportedException ();
					
				while (!(result = (-1 != zmq.getsockopt(this._socketPtr, (int)option, optionValue, optionLengthP.Ptr)))) {
					var error = ZError.GetLastErr();

					if (error == ZError.EINTR) {
						continue;
					}

					throw new ZException (error);
				}
				
				if (IntPtr.Size == 4)
					optionLength = Marshal.ReadInt32(optionLengthP.Ptr);
				else if (IntPtr.Size == 8)
					optionLength = (int)Marshal.ReadInt64(optionLengthP.Ptr);
				else 
					throw new PlatformNotSupportedException ();
			}
			return result;
		}
		
		public bool GetOption(ZSocketOption option, out byte[] value)
		{
			bool result = false;
			value = null;

			int optionLength = MaxBinaryOptionSize;
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				result = GetOption(option, optionValue, ref optionLength);
				if (result) {
					value = new byte[optionLength];
					Marshal.Copy(optionValue, value, 0, optionLength);
				}
			}

			return result;
		}

		public byte[] GetOptionBytes(ZSocketOption option) {
			byte[] result;
			if (GetOption(option, out result)) {
				return result;
			}
			return null;
		}

		public bool GetOption(ZSocketOption option, out string value)
		{
			bool result = false;
			value = null;

			int optionLength = MaxBinaryOptionSize;
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				result = GetOption(option, optionValue, ref optionLength);
				if (result) {
					value = Marshal.PtrToStringAnsi(optionValue, optionLength);
				}
			}

			return result;
		}

		public string GetOptionString(ZSocketOption option) {
			string result;
			if (GetOption(option, out result)) {
				return result;
			}
			return null;
		}

		public bool GetOption(ZSocketOption option, out Int32 value)
		{
			bool result = false;
			value = default(Int32);

			int optionLength = Marshal.SizeOf(typeof(Int32));
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				result = GetOption(option, optionValue.Ptr, ref optionLength);

				if (result) {
					value = Marshal.ReadInt32(optionValue.Ptr);
				}
			}

			return result;
		}

		public Int32 GetOptionInt32(ZSocketOption option) {
			Int32 result;
			if (GetOption(option, out result)) {
				return result;
			}
			return default(Int32);
		}

		public bool GetOption(ZSocketOption option, out UInt32 value)
		{
			Int32 resultValue;
			bool result = GetOption(option, out resultValue);
			value = (UInt32)resultValue;
			return result;
		}

		public UInt32 GetOptionUInt32(ZSocketOption option) {
			UInt32 result;
			if (GetOption(option, out result)) {
				return result;
			}
			return default(UInt32);
		}

		public bool GetOption(ZSocketOption option, out Int64 value)
		{
			bool result = false;
			value = default(Int64);

			int optionLength = Marshal.SizeOf(typeof(Int64));
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				result = GetOption(option, optionValue.Ptr, ref optionLength);
				if (result) {
					value = Marshal.ReadInt64(optionValue);
				}
			}

			return result;
		}

		public Int64 GetOptionInt64(ZSocketOption option) {
			Int64 result;
			if (GetOption(option, out result)) {
				return result;
			}
			return default(Int64);
		}

		public bool GetOption(ZSocketOption option, out UInt64 value)
		{
			Int64 resultValue;
			bool result = GetOption(option, out resultValue);
			value = (UInt64)resultValue;
			return result;
		}

		public UInt64 GetOptionUInt64(ZSocketOption option) {
			UInt64 result;
			if (GetOption(option, out result)) {
				return result;
			}
			return default(UInt64);
		}


		private bool SetOption(ZSocketOption option, IntPtr optionValue, int optionLength)
		{
			EnsureNotDisposed();

			bool result = false;

			while (!(result = (-1 != zmq.setsockopt(this._socketPtr, (int)option, optionValue, optionLength)))) {
				var error = ZError.GetLastErr();

				if (error == ZError.EINTR) {
					continue;
				}

				throw new ZException (error);
			}
			return result;
		}

		public bool SetOptionNull(ZSocketOption option) {

			return SetOption(option, IntPtr.Zero, 0);
		}

		public bool SetOption(ZSocketOption option, byte[] value) {

			bool result = false;

			if (value == null) {
				return result = SetOptionNull(option);
			}

			int optionLength = /* Marshal.SizeOf(typeof(byte)) * */ value.Length;
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				Marshal.Copy(value, 0, optionValue.Ptr, optionLength);

				result = SetOption(option, optionValue.Ptr, optionLength);
			}

			return result;
		}

		public bool SetOption(ZSocketOption option, string value) {

			bool result = false;

			if (value == null) {
				return result = SetOptionNull(option);
			}

			int optionLength;
			using (var optionValue = DispoIntPtr.AllocString(value, out optionLength))
			{
				result = SetOption(option, optionValue, optionLength);
			}

			return result;
		}

		public bool SetOption(ZSocketOption option, Int32 value) {
			
			bool result = false;

			int optionLength = Marshal.SizeOf(typeof(Int32));
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				Marshal.WriteInt32(optionValue, value);

				result = SetOption(option, optionValue.Ptr, optionLength);
			}

			return result;
		}

		public bool SetOption(ZSocketOption option, UInt32 value) {
			return SetOption(option, (Int32)value);
		}

		public bool SetOption(ZSocketOption option, Int64 value) {

			bool result = false;

			int optionLength = Marshal.SizeOf(typeof(Int64));
			using (var optionValue = DispoIntPtr.Alloc(optionLength))
			{
				Marshal.WriteInt64(optionValue, value);

				result = SetOption(option, optionValue.Ptr, optionLength);
			}

			return result;
		}

		public bool SetOption(ZSocketOption option, UInt64 value) {
			return SetOption(option, (Int64)value);
		}

        /// <summary>
        /// Subscribe to all messages.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
        public void SubscribeAll()
        {
            Subscribe(new byte[0]);
        }

        /// <summary>
        /// Subscribe to messages that begin with a specified prefix.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <param name="prefix">Prefix for subscribed messages.</param>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
        public virtual void Subscribe(byte[] prefix)
        {
			EnsureSubscriptionSocket();
            
			SetOption(ZSocketOption.SUBSCRIBE, prefix);
        }

        /// <summary>
        /// Unsubscribe from all messages.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
        public void UnsubscribeAll()
        {
            Unsubscribe(new byte[0]);
        }

        /// <summary>
        /// Unsubscribe from messages that begin with a specified prefix.
        /// </summary>
        /// <remarks>
        /// Only applies to <see cref="ZeroMQ.ZSocketType.SUB"/> and <see cref="ZeroMQ.ZSocketType.XSUB"/> sockets.
        /// </remarks>
        /// <param name="prefix">Prefix for subscribed messages.</param>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        /// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
        public virtual void Unsubscribe(byte[] prefix)
        {
			EnsureSubscriptionSocket();

			SetOption(ZSocketOption.UNSUBSCRIBE, prefix);
        }

        /// <summary>
        /// Add a filter that will be applied for each new TCP transport connection on a listening socket.
        /// Example: "127.0.0.1", "mail.ru/24", "::1", "::1/128", "3ffe:1::", "3ffe:1::/56"
        /// </summary>
        /// <seealso cref="ClearTcpAcceptFilter"/>
        /// <remarks>
        /// If no filters are applied, then TCP transport allows connections from any IP. If at least one
        /// filter is applied then new connection source IP should be matched.
        /// </remarks>
        /// <param name="filter">IPV6 or IPV4 CIDR filter.</param>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filter"/> is empty string.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        public void AddTcpAcceptFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
				throw new ArgumentException("IsNullOrWhiteSpace", "filter");
            }

            SetOption(ZSocketOption.TCP_ACCEPT_FILTER, filter);
        }

        /// <summary>
        /// Reset all TCP filters assigned by <see cref="AddTcpAcceptFilter"/> and allow TCP transport to accept connections from any IP.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
        public void ClearTcpAcceptFilter()
        {
			SetOption(ZSocketOption.TCP_ACCEPT_FILTER, (string)null);
        }
		

		/// <summary>
		/// Gets or sets the I/O thread affinity for newly created connections on this socket.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public ulong Affinity
		{
			get { return GetOptionUInt64(ZSocketOption.AFFINITY); }
			set { SetOption(ZSocketOption.AFFINITY, value); }
		}

		/// <summary>
		/// Gets or sets the maximum length of the queue of outstanding peer connections. (Default = 100 connections).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int Backlog
		{
			get { return GetOptionInt32(ZSocketOption.BACKLOG); }
			set { SetOption(ZSocketOption.BACKLOG, value); }
		}

		/// <summary>
		/// Gets or sets the identity of the current socket.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public byte[] Identity
		{
			get { return GetOptionBytes(ZSocketOption.IDENTITY); }
			set { SetOption(ZSocketOption.IDENTITY, value); }
		}

		/// <summary>
		/// Gets or sets the linger period for socket shutdown. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TimeSpan Linger
		{
			get { return TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.LINGER)); }
			set { SetOption(ZSocketOption.LINGER, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the maximum size for inbound messages (bytes). (Default = -1, no limit).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public long MaxMessageSize
		{
			get { return GetOptionInt64(ZSocketOption.MAX_MSG_SIZE); }
			set { SetOption(ZSocketOption.MAX_MSG_SIZE, value); }
		}

		/// <summary>
		/// Gets or sets the time-to-live field in every multicast packet sent from this socket (network hops). (Default = 1 hop).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int MulticastHops
		{
			get { return GetOptionInt32(ZSocketOption.MULTICAST_HOPS); }
			set { SetOption(ZSocketOption.MULTICAST_HOPS, value); }
		}

		/// <summary>
		/// Gets or sets the maximum send or receive data rate for multicast transports (kbps). (Default = 100 kbps).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int MulticastRate
		{
			get { return GetOptionInt32(ZSocketOption.RATE); }
			set { SetOption(ZSocketOption.RATE, value); }
		}

		/// <summary>
		/// Gets or sets the recovery interval for multicast transports. (Default = 10 seconds).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TimeSpan MulticastRecoveryInterval
		{
			get { return TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RECOVERY_IVL)); }
			set { SetOption(ZSocketOption.RECOVERY_IVL, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the underlying kernel receive buffer size for the current socket (bytes). (Default = 0, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int ReceiveBufferSize
		{
			get { return GetOptionInt32(ZSocketOption.RCVBUF); }
			set { SetOption(ZSocketOption.RCVBUF, value); }
		}

		/// <summary>
		/// Gets or sets the high water mark for inbound messages (number of messages). (Default = 0, no limit).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int ReceiveHighWatermark
		{
			get { return GetOptionInt32(ZSocketOption.RCVHWM); }
			set { SetOption(ZSocketOption.RCVHWM, value); }
		}

		/// <summary>
		/// Gets a value indicating whether the multi-part message currently being read has more message parts to follow.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public bool ReceiveMore
		{
			get { return GetOptionInt32(ZSocketOption.RCVMORE) == 1; }
		}

		/// <summary>
		/// Gets or sets the timeout for receive operations. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TimeSpan ReceiveTimeout
		{
			get { return TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RCVTIMEO)); }
			set { SetOption(ZSocketOption.RCVTIMEO, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the initial reconnection interval. (Default = 100 milliseconds).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TimeSpan ReconnectInterval
		{
			get { return TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RECONNECT_IVL)); }
			set { SetOption(ZSocketOption.RECONNECT_IVL, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the maximum reconnection interval. (Default = 0, only use <see cref="ReconnectInterval"/>).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TimeSpan ReconnectIntervalMax
		{
			get { return TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.RECONNECT_IVL_MAX)); }
			set { SetOption(ZSocketOption.RECONNECT_IVL_MAX, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the underlying kernel transmit buffer size for the current socket (bytes). (Default = 0, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int SendBufferSize
		{
			get { return GetOptionInt32(ZSocketOption.SNDBUF); }
			set { SetOption(ZSocketOption.SNDBUF, value); }
		}

		/// <summary>
		/// Gets or sets the high water mark for outbound messages (number of messages). (Default = 0, no limit).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int SendHighWatermark
		{
			get { return GetOptionInt32(ZSocketOption.SNDHWM); }
			set { SetOption(ZSocketOption.SNDHWM, value); }
		}

		/// <summary>
		/// Gets or sets the timeout for send operations. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TimeSpan SendTimeout
		{
			get { return TimeSpan.FromMilliseconds(GetOptionInt32(ZSocketOption.SNDTIMEO)); }
			set { SetOption(ZSocketOption.SNDTIMEO, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the supported socket protocol(s) when using TCP transports. (Default = <see cref="ProtocolType.Ipv4Only"/>).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public bool IPv6Enabled
		{
			get { return GetOptionInt32(ZSocketOption.IPV6) == 1; }
			set { SetOption(ZSocketOption.IPV6, value ? 1 : 0); }
		}

		/// <summary>
		/// Gets the last endpoint bound for TCP and IPC transports.
		/// The returned value will be a string in the form of a ZMQ DSN.
		/// </summary>
		/// <remarks>
		/// Note that if the TCP host is INADDR_ANY, indicated by a *, then the
		/// returned address will be 0.0.0.0 (for IPv4).</remarks>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public string LastEndpoint
		{
			get { return GetOptionString(ZSocketOption.LAST_ENDPOINT); }
		}

		/// <summary>
		/// Sets the behavior when an unroutable message is encountered. (Default = <see cref="ZeroMQ.RouterBehavior.Discard"/>).
		/// Only applicable to the <see cref="ZeroMQ.ZSocketType.ROUTER"/> socket type.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public RouterBehavior RouterBehavior
		{
			set { SetOption(ZSocketOption.ROUTER_BEHAVIOR, (int)value); }
		}

		/// <summary>
		/// Gets or sets the override value for the SO_KEEPALIVE TCP socket option. (where supported by OS). (Default = -1, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public TcpKeepaliveBehaviour TcpKeepalive
		{
			get { return (TcpKeepaliveBehaviour)GetOptionInt32(ZSocketOption.TCP_KEEPALIVE); }
			set { SetOption(ZSocketOption.TCP_KEEPALIVE, (int)value); }
		}

		/// <summary>
		/// Gets or sets the override value for the 'TCP_KEEPCNT' socket option (where supported by OS). (Default = -1, OS default).
		/// The default value of '-1' means to skip any overrides and leave it to OS default.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int TcpKeepaliveCnt
		{
			get { return GetOptionInt32(ZSocketOption.TCP_KEEPALIVE_CNT); }
			set { SetOption(ZSocketOption.TCP_KEEPALIVE_CNT, value); }
		}

		/// <summary>
		/// Gets or sets the override value for the TCP_KEEPCNT (or TCP_KEEPALIVE on some OS). (Default = -1, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int TcpKeepaliveIdle
		{
			get { return GetOptionInt32(ZSocketOption.TCP_KEEPALIVE_IDLE); }
			set { SetOption(ZSocketOption.TCP_KEEPALIVE_IDLE, value); }
		}

		/// <summary>
		/// Gets or sets the override value for the TCP_KEEPINTVL socket option (where supported by OS). (Default = -1, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZSocket"/> has been closed.</exception>
		public int TcpKeepaliveIntvl
		{
			get { return GetOptionInt32(ZSocketOption.TCP_KEEPALIVE_INTVL); }
			set { SetOption(ZSocketOption.TCP_KEEPALIVE_INTVL, value); }
		}

		/*
        internal void InvokePollEvents(PollEvents readyEvents)
        {
            if (readyEvents.HasFlag(PollEvents.PollIn))
            {
                InvokeReceiveReady(readyEvents);
            }

            if (readyEvents.HasFlag(PollEvents.PollOut))
            {
                InvokeSendReady(readyEvents);
            }
        }

        internal PollEvents GetPollEvents()
        {
            var events = PollEvents.None;

            if (ReceiveReady != null)
            {
                events |= PollEvents.PollIn;
            }

            if (SendReady != null)
            {
                events |= PollEvents.PollOut;
            }

            return events;
        }

        private int GetLegacySocketOption<TLegacy>(SocketOption option, Func<SocketOption, TLegacy> legacyGetter)
        {
            return ZmqVersion.Current.IsAtLeast(LatestVersion) ? GetOptionInt32(option) : Convert.ToInt32(legacyGetter(option));
        }

        private void SetLegacySocketOption<TLegacy>(SocketOption option, int value, TLegacy legacyValue, Action<SocketOption, TLegacy> legacySetter)
        {
            if (ZmqVersion.Current.IsAtLeast(LatestVersion))
            {
                SetOption(option, value);
            }
            else
            {
                legacySetter(option, legacyValue);
            }
        }

        private void InvokeReceiveReady(PollEvents readyEvents)
        {
            EventPtrr<SocketEventArgs> handler = ReceiveReady;
            if (handler != null)
            {
                handler(this, new SocketEventArgs(this, readyEvents));
            }
        }

        private void InvokeSendReady(PollEvents readyEvents)
        {
            EventPtrr<SocketEventArgs> handler = SendReady;
            if (handler != null)
            {
                handler(this, new SocketEventArgs(this, readyEvents));
            }
        } */

        private void EnsureNotDisposed()
        {
			if (_disposed) {
				throw new ObjectDisposedException (GetType().FullName);
			}
		}

		private void EnsureSendSocket()
		{ /*
			if (SocketType == ZmqSocketType.REQ
			    || SocketType == ZmqSocketType.REP
			    || SocketType == ZmqSocketType.ROUTER
			    || SocketType == ZmqSocketType.DEALER
			    || SocketType == ZmqSocketType.PAIR
			    || SocketType == ZmqSocketType.PUSH
			    || SocketType == ZmqSocketType.PUB
			    || SocketType == ZmqSocketType.XPUB
			)
				return;

			if ( SocketType == ZmqSocketType.PULL
			    || SocketType == ZmqSocketType.SUB
			    || SocketType == ZmqSocketType.XSUB
			) {
				throw new InvalidOperationException("Socket type can't send: " + SocketType);
			}

			throw new InvalidOperationException("Invalid socket type specified: " + SocketType);
		*/ }

		private void EnsureReceiveSocket()
		{ /*
			if (SocketType == ZmqSocketType.REQ
			    || SocketType == ZmqSocketType.REP
			    || SocketType == ZmqSocketType.ROUTER
			    || SocketType == ZmqSocketType.DEALER
			    || SocketType == ZmqSocketType.PAIR
			    || SocketType == ZmqSocketType.PULL
			    || SocketType == ZmqSocketType.XPUB
			    || SocketType == ZmqSocketType.SUB
			    || SocketType == ZmqSocketType.XSUB
			)
				return;

			if (SocketType == ZmqSocketType.PUSH
			    || SocketType == ZmqSocketType.PUB
			) {
				throw new InvalidOperationException("Socket type can't receive: " + SocketType);
			}

			throw new InvalidOperationException("Invalid socket type specified: " + SocketType);
		*/ }

		private void EnsureSubscriptionSocket()
		{ /*
			if (SocketType == ZmqSocketType.PUB
			    || SocketType == ZmqSocketType.XPUB
			    || SocketType == ZmqSocketType.SUB
			    || SocketType == ZmqSocketType.XSUB
			)
				return;

			if (SocketType == ZmqSocketType.REQ
			    || SocketType == ZmqSocketType.REP
			    || SocketType == ZmqSocketType.ROUTER
			    || SocketType == ZmqSocketType.DEALER
			    || SocketType == ZmqSocketType.PAIR
			    || SocketType == ZmqSocketType.PUSH
			    || SocketType == ZmqSocketType.PULL
			) {
				throw new InvalidOperationException ("Socket type doesn't have subscriptions: " + SocketType);
			}

			throw new InvalidOperationException("Invalid socket type specified: " + SocketType);
		*/ }
    }
}
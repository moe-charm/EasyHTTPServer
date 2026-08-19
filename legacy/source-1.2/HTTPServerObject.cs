using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using TTFileArrangement.HTTP;

namespace TTFileArrangement.HTTP
{
	public class  AbstractExecutor
	{
		protected System.Windows.Forms.Control control;

		public AbstractExecutor(System.Windows.Forms.Control control)
		{
			this.control = control;
		}
	}

	/// <summary>
	/// HTTPServer の概要の説明です。
	/// </summary>
	public class HTTPServerObject : AbstractExecutor
	{
		private void TimeoutCallback(object state, bool timedOut) 
		{ 
			if (timedOut) 
			{
				//System.Windows.Forms.MessageBox.Show("ここにきたよ");
				this.tcpClient.Close();
			}
			mre.Set();
		}

		public delegate void NormalDelegate(object sender, System.EventArgs e);

		#region フィールド

		protected ManualResetEvent mre = new ManualResetEvent(false);
		protected System.Net.Sockets.NetworkStream networkStream;
		protected TcpClient tcpClient;
		
		public event NormalDelegate SocketClosed;

		//public NormalDelegate SocketClosed;

		#endregion

	


		public TcpClient TcpClient
		{
			get
			{
				return this.tcpClient;
			}
			set
			{
				this.tcpClient = value;
			}
		}

	

		public HTTPServerObject(System.Windows.Forms.Control form1) : base(form1)

		{
			// 
			// TODO: コンストラクタ ロジックをここに追加してください。
			//
		}

		protected void SendData(System.Net.Sockets.NetworkStream stream,
			string msg,
			System.Text.Encoding enc)
		{
			//byte型配列に変換
			byte[] data = enc.GetBytes(msg);
			//送信
			try
			{
				mre.Reset();
				ThreadPool.RegisterWaitForSingleObject(mre, new WaitOrTimerCallback(TimeoutCallback), null, 30000, true);
				stream.Write(data, 0, data.Length);
				mre.Set();
			}
			catch(System.Exception ex)
			{
				mre.Set();
				this.Close();
				throw new Exception("SendError");
			}
		}

		protected void SendData(System.Net.Sockets.NetworkStream stream,
			byte[] msg)
		{
			try
			{
				mre.Reset();
				ThreadPool.RegisterWaitForSingleObject(mre, new WaitOrTimerCallback(TimeoutCallback), null, 30000, true);
				stream.Write(msg, 0, msg.Length);
				mre.Set();
			}
			catch(System.Exception ex)
			{
				mre.Set();
				this.Close();
				throw new Exception("SendError");
			}
		}



	
		public void Close()
		{
			this.networkStream.Close();
			this.tcpClient.Close();
			if (this.SocketClosed != null)
			{
				if (this.control != null)
				{
					this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
				}
				else
				{
					this.SocketClosed(this, System.EventArgs.Empty);
				}
			}
			return;
		}

		protected string GetReadLine(NetworkStream networkStream)
		{
			byte b1 = 0;
			byte b2;
			int i1;
			System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
			string readLine = "";
			bool flag = false;

			while(true)
			{
				try
				{
					//mre.Reset();
					//ThreadPool.RegisterWaitForSingleObject(mre, new WaitOrTimerCallback(TimeoutCallback), null, 30000, true);
					
					i1 = networkStream.ReadByte();

					if (i1 == -1)
					{
						this.Close();
						return "";
					}
					b2 = (byte)i1;
					/*
					if (networkStream.DataAvailable == false)
					{
						if (flag == true)
						{
							//this.Close();
						}
						flag = true;
					}
					*/
					memoryStream.WriteByte(b2);
					if (b1 == 0x0D && b2 == 0x0A)
					{
						byte[] buf = memoryStream.GetBuffer();
						byte[] buf2 = new Byte[memoryStream.Length - 2];
						for(int index = 0; index < buf2.Length; index++)
							buf2[index] = buf[index];
						int z1 = buf.Length;
						int z2 = buf2.Length;
						readLine = System.Text.Encoding.GetEncoding(932).GetString(buf2);
						return readLine;
					}
					b1 = b2;
				}
				catch
				{
					this.Close();
					return "";
				}
			}
		}
	}
}

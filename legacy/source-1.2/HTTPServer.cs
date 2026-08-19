using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using TTFileArrangement.HTTP;
using System.Globalization;

namespace TTFileArrangement
{

	/// <summary>
	/// HTTPServer の概要の説明です。
	/// </summary>
	public class HTTPServer : HTTPServerObject
	{
		/*
		private void TimeoutCallback(object state, bool timedOut) 
		{ 
			if (timedOut) 
			{
				this.tcpClient.Close();
			}
			mre.Set();
		}
		*/

		//public delegate void NormalDelegate(object sender, System.EventArgs e);

		#region フィールド

		//ManualResetEvent mre = new ManualResetEvent(false);
		private ContentTypeCollection contentTypeCollection = null;
		private string ipAddress;
		private CGICollection cgiCollection = null;
		private bool cgiRun = false;
		private System.Collections.Hashtable hashIDPASS;
		private bool basicAttestation = false;
		private object tag = null;
		private FileSendStatus fileSendStatus = null;
		private string firstHTML;
		private string lastHTML;
		private string rootFileName;
		private string [] folderPaths;
		//private System.Net.Sockets.NetworkStream networkStream;
		
		//private TcpClient tcpClient;
		
		public event NormalDelegate RequestGeted;
		public event NormalDelegate ResponseSended;
		//new public NormalDelegate SocketClosed;
		//new public  event NormalDelegate SocketClosed;
		//public event NormalDelegate SocketClosed;

		#endregion

		#region プロパティ

		public ContentTypeCollection ContentTypeCollection
		{
			set
			{
				this.contentTypeCollection = value;
			}
			get
			{
				return this.contentTypeCollection;
			}
		}

		public string IPAddress
		{
			set
			{
				this.ipAddress = value;
			}
			get
			{
				return this.ipAddress;
			}
		}

		public CGICollection CGICollection
		{
			set
			{
				this.cgiCollection = value;
			}
			get
			{
				return this.cgiCollection;
			}
		}

		public bool CGIRun
		{
			set
			{
				this.cgiRun = value;
			}
			get
			{
				return this.cgiRun;
			}
		}

		public System.Collections.Hashtable HashIDPASS
		{
			set
			{
				this.hashIDPASS = value;
			}
			get
			{
				return this.hashIDPASS;
			}
		}

		public bool BasicAttestation
		{
			get
			{
				return this.basicAttestation;
			}
			set
			{
				this.basicAttestation = value;
			}
		}

		public object Tag
		{
			set
			{
				this.tag = value;
			}
			get
			{
				return this.tag;
			}
		}
		
			public FileSendStatus FileSendStatusProperty
		{
			set
			{
				lock(this)
				{
					this.fileSendStatus = value;
				}
			}
			get
			{
				lock(this)
				{
					return this.fileSendStatus;
				}
			}
		}

		public string[] FirstHTML
		{
			set
			{
				foreach(string htmlLine in value)
				{
					this.firstHTML += htmlLine + "\r\n";
				}	
			}
		}

		public string[] LastHTML
		{
			set
			{
				foreach(string htmlLine in value)
				{
					this.lastHTML += htmlLine + "\r\n";
				}	
			}
			
		}

		public string RootFileName
		{
			set
			{
				this.rootFileName = value;
			}
			get
			{
				return this.rootFileName;
			}
		}

		public string [] FolderPaths
		{
			set
			{
				this.folderPaths = value;
			}
			get
			{
				return folderPaths;
			}
		}
/*
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
*/
		#endregion

		public HTTPServer(System.Windows.Forms.Control form1) : base(form1)

		{
			// 
			// TODO: コンストラクタ ロジックをここに追加してください。
			//
		}

		public void ListenStart()
		{

			HTTPCommand httpCommand;
			tcpClient.ReceiveTimeout = 30000;
			tcpClient.SendTimeout = 30000;
			networkStream = tcpClient.GetStream();

			string readLine;

			

			
			
			while (true)
			{
				try
				{
					while(true)
					{
						httpCommand = new HTTPCommand();
						//readLine = streamReader.ReadLine();
						readLine = this.GetReadLine(networkStream);
						
						if (readLine == "" || readLine == null)
						{
							this.Close();
							/*if (this.SocketClosed != null)
								this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
							*/
							return;
						}
						else
							break;
					}

					httpCommand.Requst = readLine;

					
				}
				catch(Exception e)
				{
					this.Close();
					/*if (this.SocketClosed != null)
					{
						this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
					}*/
					return;
				}

				
		
				do
				{	
					try
					{
						//readLine = streamReader.ReadLine();
						readLine = this.GetReadLine(networkStream);
						httpCommand.MessageHeaders.Add(readLine);
					}
					catch
					{
						this.Close();
						/*
						if (this.SocketClosed != null)
							this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
						
						*/
						return;
					}
			
				} while (readLine != "");

				string contentType = httpCommand.GetMessage("Content-Type");
				if (contentType != null)
				{
					//TTFA.Win32.WindowsAPI.SetEnvironmentVariable("CONTENT_TYPE", contentType);
				}


				

				if (this.RequestGeted != null)
				{
					string response;

					response = httpCommand.Requst + "\r\n";
					foreach(string message in httpCommand.MessageHeaders)
					{
						response += message + "\r\n";
					}

					if (this.control != null)
					{
						this.control.Invoke(this.RequestGeted, new object[]{response, System.EventArgs.Empty});
					}
					else
					{
						this.RequestGeted(response, System.EventArgs.Empty);
					}
				}

				

				switch(httpCommand.Method.ToLower())
				{
					case ("get"):
					{
						
						//TTFA.Win32.WindowsAPI.SetEnvironmentVariable("REQUEST_METHOD", "GET");
						
						if (this.Get(this.networkStream, httpCommand) == false)
						{
							//if (this.SocketClosed != null)
							//	this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
							this.Close();
							return;
						}
						string message = httpCommand.GetMessage("Connection");
						if (message == "close")
						{
							//if (this.SocketClosed != null)
							//	this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
							this.Close();
							return;
						}
						continue;
					}
					case("head"):
					{
						if (this.head(this.networkStream, httpCommand) == false)
						{
							/*if (this.SocketClosed != null)
								this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
							*/
							return;
						}
						string message = httpCommand.GetMessage("Connection");
						if (message == "close")
						{
							this.Close();
							/*
							if (this.SocketClosed != null)
								this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
							*/
							return;
						}
						continue;
					}
					default:
					{
						this.Close();
						
						return;
					}
					case ("post"):
					{
						string filePath = GetPathFromURL(httpCommand.UrlPath);

						if (this.cgiRun == false)
						{
							this.Close();

							return;
						}
					
						
						continue;
					}
				}
			}
		}

		public void ParentControlEvent(System.EventHandler e, object sender)
		{
			object [] args = { sender, System.EventArgs.Empty };

			
			this.control.Invoke(e , args);
		}

		private void SetProsessEnvironmentVariable(System.Diagnostics.Process process, HTTPCommand httpCommand, string filePath)
		{
			string contentsLengthString = httpCommand.GetMessage("Content-Length");

			if (contentsLengthString != null)
			{
				process.StartInfo.EnvironmentVariables.Add("CONTENT_LENGTH", contentsLengthString);
			}

			process.StartInfo.EnvironmentVariables.Add("REQUEST_METHOD", httpCommand.Method);

			if (this.CGIRun == true)
			{
				foreach(CGI cgi in this.cgiCollection)
				{
					if (cgi.Enable == true)
					{
						foreach(TTFA.CommonClass.CharSplitString extension in cgi.Extensions)
						{
							if(System.IO.Path.GetExtension(filePath).ToLower() == "." + extension.SplitString)
							{
								if (cgi.PATH_TRANSLATED == true)
								{
									process.StartInfo.EnvironmentVariables.Add("PATH_TRANSLATED", filePath);
									break;
								}
							}
						}
					}
				}

				string authorization = httpCommand.GetMessage("Authorization");
				Regex r;
				Match m;
				string idpassBase64;
				byte [] bs;
				string[] idPass;

				if	(authorization != null)
				{

					r = new Regex(@"Basic\s*(?<IDPASS>.*)");
					m = r.Match(authorization);
					idpassBase64 = m.Groups["IDPASS"].Value;
					bs = System.Convert.FromBase64String(idpassBase64);
					idPass = System.Text.Encoding.ASCII.GetString(bs).Split(':');

					if (idPass.Length == 2)
					{
						process.StartInfo.EnvironmentVariables.Add("REMOTE_USER", idPass[0]);
					}
				}

				process.StartInfo.EnvironmentVariables.Add("QUERY_STRING", httpCommand.UrlArgvs);
				process.StartInfo.EnvironmentVariables.Add("HTTP_USER_AGENT", httpCommand.GetMessage("User_Agent"));
				process.StartInfo.EnvironmentVariables.Add("REMOTE_ADDR", this.IPAddress);
				process.StartInfo.EnvironmentVariables.Add("SCRIPT_FILENAME", filePath.Replace("\\", "/"));
				process.StartInfo.EnvironmentVariables.Add("SERVER_PROTOCOL", "HTTP/1.1");
				process.StartInfo.EnvironmentVariables.Add("SERVER_SOFTWARE", "TTFA");
				
				
			}	
		}

		private bool post(NetworkStream networkStream, HTTPCommand httpCommand, string filePath, CGI cgi)
		{
			string contentsLengthString = httpCommand.GetMessage("Content-Length");
			byte [] buf;
			string exePath;
			string html;

			if (cgi.FirstLineRead == true)
			{
				exePath = this.GetEXEPathFromCGI(filePath);
			}
			else if (cgi.ExePath != "")
			{
				exePath = cgi.ExePath;
			}
			else
			{
				return false;
			}

			html = "HTTP/1.1 200 Document follows"  + "\r\n"
				+ "Server: TTFA"  + "\r\n"
				+ "Connection: close" + "\r\n";

			
			if (contentsLengthString != null)
			{
				long contentsLength = long.Parse(contentsLengthString);
				
				buf = new Byte[contentsLength];
				networkStream.Read(buf, 0, buf.Length);

				string sendtext = System.Text.Encoding.GetEncoding(932).GetString(buf);
				long sss = sendtext.Length;
				//TTFA.Win32.WindowsAPI.SetEnvironmentVariable("CONTENT_LENGTH", contentsLengthString);
				
				
				try
				{
					System.IO.Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(filePath));
				}
				catch
				{
				}

				//TTFA.Win32.WindowsAPI.SetEnvironmentVariable("QUERY_STRING", httpCommand.UrlArgvs);

				System.Diagnostics.Process p = new System.Diagnostics.Process();
				SetProsessEnvironmentVariable(p, httpCommand, filePath);
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.FileName = exePath;
				p.StartInfo.Arguments = System.IO.Path.GetFileName(filePath) + " " + httpCommand.UrlArgvs;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardInput = true;
				p.Start();
				//p.StandardInput.Write(sendtext);
				p.StandardInput.BaseStream.Write(buf, 0, buf.Length);
				p.StandardInput.Close();
				
				
				System.IO.BinaryReader reader = new System.IO.BinaryReader(p.StandardOutput.BaseStream);
				//string z2 = p.StandardOutput.ReadToEnd();

				
				byte [] buf2;
				System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
				do
				{
					buf2 = reader.ReadBytes(1000);
					memoryStream.Write(buf2, 0, buf2.Length);
				}while(buf2.Length == 1000);

				this.SendData(networkStream, html, System.Text.Encoding.ASCII);
				this.SendData(networkStream, memoryStream.GetBuffer());
				reader.Close();
				//string zzz = System.Text.Encoding.GetEncoding(932).GetString(memoryStream.GetBuffer());
				//this.SendData(networkStream, sendtext, System.Text.Encoding.GetEncoding(932));
				
				return false;
			}
			return false;
		}

		private bool head(NetworkStream networkStream, HTTPCommand httpCommand)
		{
			string urlPath;
			string path;
 
			urlPath =  System.Web.HttpUtility.UrlDecode(httpCommand.Uri);

			path = GetPathFromURL(urlPath);

			if (path == "")
			{
				return Send404(networkStream, httpCommand);
			}
			else
			{
				try
				{
					this.SendResponse(networkStream, "HTTP/1.1 200 OK" + "\r\n" + "\r\n");
				}
				catch
				{
					return false;
				}
				return true;
			}
		}

		private bool Get(NetworkStream networkStream, HTTPCommand httpCommand)
		{
			bool result;
			string html;
			string urlPath;
			string path;

			

			if (this.basicAttestation == true)
			{
				string authorization = httpCommand.GetMessage("Authorization");
				Regex r;
				Match m;
				string idpassBase64;
				byte [] bs;
				string[] idPass;
				//string id;

				if	(authorization == null)
					return this.Send401(networkStream, httpCommand);

				r = new Regex(@"Basic\s*(?<IDPASS>.*)");
				m = r.Match(authorization);
				idpassBase64 = m.Groups["IDPASS"].Value;
				bs = System.Convert.FromBase64String(idpassBase64);
				idPass = System.Text.Encoding.ASCII.GetString(bs).Split(':');

				if (idPass.Length != 2)
					return this.Send401(networkStream, httpCommand);
				
				string pass = (string)this.hashIDPASS[idPass[0]];

				if (idPass[1] != pass)
					return this.Send401(networkStream, httpCommand);
			}
				

			
			

			if (this.rootFileName != "" && httpCommand.Uri == "/")
			{
				return this.Send404(networkStream, httpCommand);
			}
			if (httpCommand.Uri == "/" + this.rootFileName)
			{
				html = this.GetTopHTMLText();
				return SendHTML(this.networkStream, html);
			}
			
			urlPath = httpCommand.UrlPath;

			path = GetPathFromURL(urlPath);

			

			if (path != null)
			{
				//CGI
				if (this.CGIRun == true)
				{
					foreach(CGI cgi in this.cgiCollection)
					{
						if (cgi.Enable == true)
						{
							foreach(TTFA.CommonClass.CharSplitString extension in cgi.Extensions)
							{
								if(System.IO.Path.GetExtension(path).ToLower() == "." + extension.SplitString)
								{
									return this.SendCGI(networkStream, path, httpCommand, cgi);
								}
							}
						}
					}
				}
				/*
				if (this.cgiRun == true && System.IO.Path.GetExtension(path).ToLower() == ".cgi")
				{
					return this.SendCGI(networkStream, path, httpCommand);
				}
				else if (this.cgiRun == true && System.IO.Path.GetExtension(path).ToLower() == ".php")
				{
					return this.SendPHP(networkStream, path, httpCommand);
				}
				*/


				result = this.SendFile(networkStream, path, httpCommand);
				this.FileSendStatusProperty = null;
				return result;
			}

			if (System.IO.Path.GetFileName(urlPath).ToLower() == "index.html")
			{
				foreach(string folderPath in this.folderPaths)
				{
					string folderName = System.IO.Path.GetFileName(folderPath);
					
					if (urlPath.Length < folderName.Length + 1)
						continue;
					string zzz = urlPath.Substring(0, folderName.Length + 1);
					if ("/" + folderName == urlPath.Substring(0, folderName.Length + 1))
					{
						string folderPath2 = "";
						string folderPath3 = "";
						int ind1 = urlPath.LastIndexOf('/');
						int ind2 = folderPath.LastIndexOf('\\');
						if (folderPath[folderPath.Length - 1] != '\\')
						{
							folderPath2 = folderPath.Substring(0, ind2);
							folderPath2 = folderPath2 + urlPath.Substring(0, ind1);
							folderPath3 = folderPath;
						}
						else
						{
							folderPath2 = urlPath.Substring(1, ind1);
							folderPath3 = folderPath;
						}
			
						
						if (!System.IO.Directory.Exists(folderPath2))
							continue;
						html = GetFolderIndexHtml(folderPath3, folderPath2);
						if (html == null)
						{
							this.Send404(networkStream, httpCommand);
							return true;
						}
						return SendHTML(this.networkStream, html);
					}
						
				}
			}
			return this.Send404(networkStream, httpCommand);
		}

		private string GetFolderPath(string urlPath)
		{
			return "";
		}

		private string GetPathFromURL(string urlPath)
		{
			bool fileExists = false;
			string path = "";
			foreach(string folderPath in this.folderPaths)
			{
				if (folderPath[folderPath.Length - 1] != '\\')
				{
					string folderName = System.IO.Path.GetFileName(folderPath);	
					if (urlPath.Length <= folderName.Length)
						continue;
					if (urlPath.Substring(1, folderName.Length) != folderName)
						continue;
					path = folderPath + urlPath.Substring(folderName.Length + 1);
					fileExists = System.IO.File.Exists(path);
				}
				else
				{
					if (urlPath.Length <= folderPath.Length)
						continue;
					if (urlPath.Substring(1, folderPath.Length - 1) != folderPath.Substring(0, folderPath.Length -1))
						continue;
					path = folderPath + urlPath.Substring(folderPath.Length + 1);
					fileExists = System.IO.File.Exists(path);
				}
				if (fileExists == true)
					return path;
			}
			return null;
		}
		
		private string GetFolderIndexHtml(string rootFolder, string folderPath)
		{
			string html;
			int length = 0;

			if (rootFolder[rootFolder.Length - 1] == '\\')
				length = rootFolder.Length;
			else
				length = rootFolder.Length + 1;

			System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(folderPath);
			try
			{
				html = this.firstHTML;
				foreach(System.IO.DirectoryInfo childDirectoryInfo in directoryInfo.GetDirectories())
				{
					
					string folderPath2 = childDirectoryInfo.FullName.Substring(length);
					html += "<div class = \"folder\">\r\n"
						+ @"<a href=""./"
						+ System.Web.HttpUtility.UrlEncode(System.IO.Path.GetFileName(folderPath2))
						+ @"/index.html"">"
						+ System.IO.Path.GetFileName(folderPath2)
						+ "</a><br>\r\n"
						+ "</div>\r\n";
				}
			}
			catch
			{
				return null;
			}

			foreach(System.IO.FileInfo childFileInfo in directoryInfo.GetFiles())
			{
				string fileSize = String.Format("{0:N0}", childFileInfo.Length);
				string filePath = childFileInfo.FullName.Substring(length);
				html += "<div class = \"file\">\r\n"
					+ @"<a style = ""text-decoration:none;"" href=""./"
					+ System.Web.HttpUtility.UrlEncode(System.IO.Path.GetFileName(filePath))
					+ @""">"
					+ System.IO.Path.GetFileName(filePath)
					+ "</a><br>\r\n"
					+ "</div>\r\n"
					+ "<div class = \"filesize\">\r\n"
					+ (childFileInfo.Length / (1024 * 1024)).ToString() + "Mbyte " + fileSize + "\r\n"
					+ "</div>\r\n";
			}

			html += @"<a href=""../"
				+ @"index.html"">"
				+ ".."
				+ "</a><br>\r\n";

			html += this.lastHTML;

			return html;
		}

		private string GetTopHTMLText()
		{
			string htmlText = "";

			htmlText = this.firstHTML;

			foreach(string folderPath in folderPaths)
			{
				if (folderPath[folderPath.Length -1] != '\\')
				{
					htmlText += "<div class = \"folder\">\r\n"
						+ @"<a href=""./"
						+ System.Web.HttpUtility.UrlEncode(System.IO.Path.GetFileName(folderPath))
						+ @"/index.html"">"
						+ System.IO.Path.GetFileName(folderPath)
						+ "</a>\r\n"
						+ "</div>\r\n";
				}
				else
				{
					htmlText += "<div class = \"folder\">\r\n"
						+ @"<a href=""./"
						+ System.Web.HttpUtility.UrlEncode(folderPath.Substring(0, folderPath.Length - 1))
						+ @"/index.html"">"
						+ folderPath.Substring(0, folderPath.Length - 1)
						+ "</a>\r\n"
						+ "</div>\r\n";
				}
			}

			htmlText += this.lastHTML;

			return htmlText;
		}

		private string GetContentType(string filePath)
		{
			foreach(ContentType ct in this.contentTypeCollection)
			{
				string ctExtention = "." + ct.Extension.ToLower();
				string filePathExtention = System.IO.Path.GetExtension(filePath).ToLower();
				if ( ctExtention == filePathExtention )
				{
					return ct.ContentName;
				}
			}

			return "application/octet-stream";
		}

		#region Send

		private bool SendCGI(NetworkStream networkStream, string filePath, HTTPCommand httpCommand, CGI cgi)
		{
			
			string html;
			string exePath;

			if (cgi.FirstLineRead == true)
			{
				exePath = this.GetEXEPathFromCGI(filePath);
			}
			else if (cgi.ExePath != "")
			{
				exePath = cgi.ExePath;
			}
			else
			{
				return false;
			}

			if (exePath == null)
				return false;

			html = "HTTP/1.1 200 OK"  + "\r\n"
				+ "Server: TTFA"  + "\r\n"
				+ "Connection: " + httpCommand.GetMessage("Connection")  + "\r\n";
			try
			{
				System.IO.Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(filePath));

				//TTFA.Win32.WindowsAPI.SetEnvironmentVariable("QUERY_STRING", httpCommand.UrlArgvs);

				System.Diagnostics.Process p = new System.Diagnostics.Process();
				SetProsessEnvironmentVariable(p, httpCommand, filePath);
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.FileName = exePath;
				p.StartInfo.Arguments = System.IO.Path.GetFileName(filePath) + " " + httpCommand.UrlArgvs;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.Start();
				System.IO.BinaryReader reader = new System.IO.BinaryReader(p.StandardOutput.BaseStream);
			
				byte [] buf;
				System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
				do
				{
					buf = reader.ReadBytes(1000);
					
					memoryStream.Write(buf, 0, buf.Length);
				}while(buf.Length == 1000);

				this.SendData(networkStream, html, System.Text.Encoding.ASCII);
				this.SendData(networkStream, memoryStream.GetBuffer());
				reader.Close();
				string zzz = System.Text.Encoding.GetEncoding(932).GetString(memoryStream.GetBuffer());

				return false;
			}
			catch
			{
				return false;
			}
			

			/*
			string html;
			string exePath;

			exePath = this.GetEXEPathFromCGI(filePath);

			if (exePath == null)
				return false;

			html = "HTTP/1.1 200 OK"  + "\r\n"
				+ "Server: TTFA"  + "\r\n"
				+ "Connection: " + httpCommand.GetMessage("Connection")  + "\r\n";
			try
			{
				System.IO.Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(filePath));

				TTFA.Win32.WindowsAPI.SetEnvironmentVariable("QUERY_STRING", httpCommand.UrlArgvs);

				System.Diagnostics.Process p = new System.Diagnostics.Process();
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.FileName = exePath;
				p.StartInfo.Arguments = System.IO.Path.GetFileName(filePath) + " " + httpCommand.UrlArgvs;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.Start();
				System.IO.BinaryReader reader = new System.IO.BinaryReader(p.StandardOutput.BaseStream);
			
				byte [] buf;
				System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
				do
				{
					buf = reader.ReadBytes(1000);
					memoryStream.Write(buf, 0, buf.Length);
				}while(buf.Length == 1000);

				this.SendData(networkStream, html, System.Text.Encoding.ASCII);
				this.SendData(networkStream, memoryStream.GetBuffer());
				string zzz = System.Text.Encoding.GetEncoding(932).GetString(memoryStream.GetBuffer());

				return false;
			}
			catch
			{
				return false;
			}
			return false;
			*/
		}

		

		private bool SendFile(NetworkStream networkStream, string filePath, HTTPCommand httpCommand)
		{
			//bool result;
			this.FileSendStatusProperty = new FileSendStatus();
			this.FileSendStatusProperty.FileName = httpCommand.FileName;

			string rangeMessage = httpCommand.GetMessage("Range");

			if (rangeMessage == null)
			{
				return SendFile200(networkStream, filePath, httpCommand);
			}
			
			Regex r = new Regex(@"bytes\s*=\s*(?<first>\d*|.*)-(?<last>\d*|.*)");
			Match m = r.Match(rangeMessage);

			string first = m.Groups["first"].Value;
			string last = m.Groups["last"].Value;

			if (first == "" && last == "")
			{
				return Send416(networkStream, httpCommand);
			}

			long firstPos;
			long lastPos;

			try
			{
				if (first == "")
				{
					firstPos = 0;
				}
				else
				{
					firstPos = long.Parse(first);
				}

				if (last == "")
				{
					System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
					lastPos = fileInfo.Length - 1;
				}
				else
				{
					lastPos = long.Parse(last);
				}
			}
			catch
			{
				return Send416(networkStream, httpCommand);
			}

			if (firstPos > lastPos)
			{
				return Send416(networkStream, httpCommand);
			}

			return SendFile206(networkStream, filePath, httpCommand, rangeMessage, firstPos, lastPos);
		}

		private bool SendFile206(NetworkStream networkStream, string filePath, HTTPCommand httpCommand, string rangeMessage, long firstPos, long lastPos)
		{
			System.IO.BinaryReader reader = null;
			System.IO.BinaryWriter writer = null;

			try
			{
				string html;
				long fileSize;
				long sendFileSize;
				string firstPosString;
				string lastPosString;
				string contentType;
				string gmt1;
				string gmt2;

				System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);

				gmt1 = this.GetGMTFromDate(System.DateTime.Now);
				gmt2 = this.GetGMTFromDate(fileInfo.LastWriteTime);
				sendFileSize = lastPos - firstPos + 1;
				firstPosString = firstPos.ToString();
				lastPosString = lastPos.ToString();

				fileSize = fileInfo.Length;

				fileSendStatus.FilePosStart = firstPos;
				fileSendStatus.FilePosEnd = lastPos;

				contentType = this.GetContentType(filePath);
				html = "HTTP/1.1 206 Partial Content"  + "\r\n"
					+ "Date: " + gmt1 + "\r\n"
					+ "Last-Modified: " + gmt2 + "\r\n"
					+ "Content-Type: application/octet-stream"  + "\r\n"
					+ "Accept-Ranges: bytes"  + "\r\n"
					+ "Content-Range: bytes " + firstPosString + "-" + lastPosString + "/" + fileInfo.Length.ToString() + "\r\n"
					+ "Content-Length: " + (string)(lastPos - firstPos + 1).ToString() + "\r\n"
					+ "Content-Type: " + contentType + "\r\n"
					+ "Server: TTFA"  + "\r\n"
					+ "Connection: " + httpCommand.GetMessage("Connection")  + "\r\n"
					+ "\r\n";

				this.SendResponse(networkStream, html);

				reader = new System.IO.BinaryReader(fileInfo.OpenRead());
				writer = new System.IO.BinaryWriter(networkStream);

				reader.BaseStream.Seek(firstPos, System.IO.SeekOrigin.Begin);

				int bufSize = 10000;
				long count = sendFileSize / bufSize;

				byte[] buf;
				for(int index = 0; index < count; index++)
				{
					buf = reader.ReadBytes(bufSize);
					writer.Write(buf);
					fileSendStatus.FilePos = reader.BaseStream.Position;
				}
				
				buf = reader.ReadBytes((int)(sendFileSize - bufSize * count));
				writer.Write(buf);
				fileSendStatus.FilePos = reader.BaseStream.Position;
				
				long z = reader.BaseStream.Position;

				reader.Close();

				

				return true;
			}
			catch
			{
				reader.Close();
				return false;
			}
		}

	
		private bool SendFile200(NetworkStream networkStream, string filePath, HTTPCommand httpCommand)
		{
			FileSendStatus fileSendStatus = this.FileSendStatusProperty;
			
			System.IO.BinaryReader reader = null;
			System.IO.BinaryWriter writer = null;

			try
			{
				string html;
				System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
				long fileSize;
				string contentType;
				string gmt1;
				string gmt2;

				gmt1 = this.GetGMTFromDate(System.DateTime.Now);
				gmt2 = this.GetGMTFromDate(fileInfo.LastWriteTime);

				fileSize = fileInfo.Length;

				fileSendStatus.FilePosEnd = fileSize - 1;

				
				contentType = this.GetContentType(filePath);

				html = "HTTP/1.1 200 OK"  + "\r\n"
					+ "Date: " + gmt1 + "\r\n"
					+ "Last-Modified: " + gmt2 + "\r\n"
					+ "Server: TTFA"  + "\r\n"
					+ "Accept-Ranges: bytes"  + "\r\n"
					+ "Connection: " + httpCommand.GetMessage("Connection")  + "\r\n"
					+ "Content-Type: " + contentType  + "\r\n"
					+ "Content-Length: " + fileSize.ToString() + "\r\n"
					+ "\r\n";

				this.SendResponse(networkStream, html);


				reader = new System.IO.BinaryReader(fileInfo.OpenRead());
				writer = new System.IO.BinaryWriter(networkStream);
			
				byte[] buf;
				do
				{
					buf = reader.ReadBytes(10000);
					writer.Write(buf);
					fileSendStatus.FilePos = reader.BaseStream.Position;
				} while(buf.Length == 10000);
				
				reader.Close();

				return true;
			}
			catch
			{
				if (reader != null)
					reader.Close();			

				return false;
			}
			
		}

		#region SendError

		private bool Send416(NetworkStream networkStream, HTTPCommand httpCommand)
		{
			try
			{
				this.SendResponse(networkStream, "HTTP/1.1 416 Requested Range Not Satisfiable" + "\r\n" + "\r\n");
			}
			catch
			{
				return false;
			}
			return true;
		}

		private bool Send401(NetworkStream networkStream, HTTPCommand httpCommand)
		{
			try
			{
				string html = this.Get401HTML(httpCommand);
				string html2;
				byte[] msg = System.Text.Encoding.GetEncoding(932).GetBytes(html);

				html2 = "HTTP/1.1 401 Unauthorized"  + "\r\n"
					+ @"WWW-Authenticate: Basic realm = ""TTFA Server"""  + "\r\n"
					+ "Server: TTFA"  + "\r\n"
					+ "Connection: Keep-Alive"  + "\r\n"
					+ "Content-Length: " + msg.Length.ToString() + "\r\n"
					+ "Content-Type: text/html"  + "\r\n"
					+ "\r\n";

				this.SendResponse(networkStream, html2);
				
				this.SendData(networkStream, msg);

				return true;
			}
			catch
			{
				return false;
			}
			
		}

		private bool Send404(NetworkStream networkStream, HTTPCommand httpCommand)
		{
			try
			{
				string html = this.Get404HTML(httpCommand);
				string html2;
				byte[] msg = System.Text.Encoding.GetEncoding(932).GetBytes(html);

				html2 = "HTTP/1.1 404 Not Found"  + "\r\n"
					+ "Server: TTFA"  + "\r\n"
					+ "Connection: Keep-Alive"  + "\r\n"
					+ "Content-Length: " + msg.Length.ToString() + "\r\n"
					+ "Content-Type: text/html"  + "\r\n"
					+ "\r\n";

				this.SendResponse(networkStream, html2);
				
				this.SendData(networkStream, msg);

				return true;
			}
			catch
			{
				return false;
			}
			
		}

		private string Get401HTML(HTTPCommand httpCommand)
		{
			string html;

			html = @"<!DOCTYPE HTML PUBLIC ""-//IETF//DTD HTML 2.0//EN"">" + "\r\n"
				+ @"<html><head>" + "\r\n"
				+ @"<title>401 Unauthorized</title>" + "\r\n"
				+ @"</head><body>" + "\r\n"
				+ @"<h1>Unauthorized</h1>" + "\r\n"
				+ @"<hr>" + "\r\n"
				+ @"<address>http://unyora.sakura.ne.jp/ Server at TTFAServer Port 80</address>" + "\r\n"
				+ @"</body></html>" + "\r\n";

			return html;
		}

		private string Get404HTML(HTTPCommand httpCommand)
		{
			string html;

			html = @"<!DOCTYPE HTML PUBLIC ""-//IETF//DTD HTML 2.0//EN"">" + "\r\n"
			+ @"<html><head>" + "\r\n"
			+ @"<title>404 Not Found</title>" + "\r\n"
			+ @"</head><body>" + "\r\n"
			+ @"<h1>Not Found</h1>" + "\r\n"
			+ httpCommand.Uri + "このURLは見つかりません<P>" + "\r\n"
			+ @"<hr>" + "\r\n"
			+ @"<address>http://unyora.sakura.ne.jp/ Server at TTFAServer Port 80</address>" + "\r\n"
			+ @"</body></html>" + "\r\n";

			return html;
		}

		#endregion

		private bool SendHTML(NetworkStream networkStream, string htmlText)
		{
			byte[] msg = System.Text.Encoding.GetEncoding(932).GetBytes(htmlText);
			

			try
			{
				string gmt1
					;
				gmt1 = this.GetGMTFromDate(System.DateTime.Now);
				string htmlText2 = "HTTP/1.1 200 OK"  + "\r\n"
					+ "Date: " + gmt1 + "\r\n"
					+ "Server: TTFA"  + "\r\n"
					+ "Connection: Keep-Alive" + "\r\n"
					+ "Content-Length: " + msg.Length.ToString() + "\r\n"
					+ "Content-Type: text/html"  + "\r\n"
					+ "\r\n";

				this.SendResponse(networkStream, htmlText2);
				
				this.SendData(networkStream, msg);

				/*
				if (this.ResponseGeted != null)
				{
					this.ResponseSended(htmlText2, System.EventArgs.Empty);
					this.ResponseSended(htmlText + "\r\n", System.EventArgs.Empty);
				}
				*/

				return true;
			}
			catch
			{
				return false;
			}
			
		}

		private  void SendResponse(System.Net.Sockets.NetworkStream stream,
			string msg)
		{
			if (this.ResponseSended != null)
			{
				if (this.control != null)
				{
					this.control.Invoke(this.ResponseSended, new object[]{msg, System.EventArgs.Empty});
				}
				else
				{
					this.ResponseSended(msg, System.EventArgs.Empty);
				}
			}
			SendData(stream, msg, System.Text.Encoding.ASCII);
		}

#if false
		private void SendData(System.Net.Sockets.NetworkStream stream,
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
				this.Close();
				throw new Exception("SendError");
			}
		}

		private void SendData(System.Net.Sockets.NetworkStream stream,
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
				this.Close();
				throw new Exception("SendError");
			}
		}
#endif
		#endregion 

		private string GetEXEPathFromCGI(string filePath)
		{
			System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
			string textLine;

			if (fileInfo.Exists == false)
				return null;

			System.IO.StreamReader streamReader = new System.IO.StreamReader(fileInfo.OpenRead());
			
			textLine = streamReader.ReadLine();

			if (textLine.Length == 0)
				return null;

			if (textLine[0] != '#')
				return null;

			textLine = @"c:"+ textLine.Substring(2) + ".exe";

			if (System.IO.File.Exists(textLine) == false)
				return null;
															
			return textLine;
		}

		private string GetGMTFromDate(System.DateTime dateTime)
		{
			CultureInfo culture = null;
			

			// カルチャーのクローンを作る。new より早い。
			culture = (CultureInfo)(Thread.CurrentThread.CurrentCulture.Clone());

			//　西暦(英語）表示にする。
			GregorianCalendar gregUSCal = new System.Globalization.GregorianCalendar();
			gregUSCal.CalendarType = GregorianCalendarTypes.USEnglish;
			culture.DateTimeFormat.Calendar = gregUSCal;
			return dateTime.ToString("r", culture);
		}

#if false
		public void Close()
		{
			this.networkStream.Close();
			this.tcpClient.Close();
			if (this.SocketClosed != null)
				this.control.Invoke(this.SocketClosed, new object[]{this, System.EventArgs.Empty});
			return;
		}

		private string GetReadLine(NetworkStream networkStream)
		{
			byte b1 = 0;
			byte b2;
			System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
			string readLine = "";

			while(true)
			{
				try
				{
					
					b2 = (byte)networkStream.ReadByte();
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
		#endif
	}




}

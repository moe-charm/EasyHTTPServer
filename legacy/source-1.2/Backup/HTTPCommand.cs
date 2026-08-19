using System;

namespace TTFileArrangement.HTTP
{
	/// <summary>
	/// HTTPCommand の概要の説明です。
	/// </summary>
	public class HTTPCommand
	{
		#region フィールド

		private string urlArgvs = "";
		private string urlPath = "";
		private string method = "";
		private string uri = "";
		private string version = "";
		private string request = "";
		private string response = "";
		private string fileName = "";
		private System.Collections.ArrayList messageHeaders = new System.Collections.ArrayList();

		#endregion

		#region プロパティ

		public string FileName
		{
			set
			{
				this.fileName = value;
			}
			get
			{
				return this.fileName;
			}
		}

		public string Method
		{
			set
			{
				this.method = value;
			}
			get
			{
				return this.method;
			}
		}

		public string UrlPath
		{
			get
			{
				return this.urlPath;
			}
		}

		public string UrlArgvs
		{
			get
			{
				return this.urlArgvs;
			}
		}

		public string Uri
		{
			set
			{
				string tmp;
				this.uri = value;
				tmp = System.Web.HttpUtility.UrlDecode(uri);
				string [] tmp2 = tmp.Split('?');
				if (tmp2.Length == 1)
				{
					this.urlPath = tmp;
					this.fileName = System.IO.Path.GetFileName(this.UrlPath);
				}
				else
				{
					this.urlPath = tmp2[0];
					this.urlArgvs = tmp2[1];
					this.fileName = System.IO.Path.GetFileName(this.UrlPath);
				}
			}
			get
			{
				return this.uri;
			}
		}

		public string Version
		{
			set
			{
				this.version = value;
			}
			get
			{
				return this.version;
			}
		}

		public string Requst
		{
			set
			{
				this.request = value;
				string [] str = this.request.Split(' ');
				if (str.Length != 3)
					return;
				this.method = str[0];
				this.Uri = str[1];
				this.version = str[2];
			}
			get
			{
				return this.request;
			}
		}

		public string Response
		{
			set
			{
				this.response = value;
			}
			get
			{
				return this.response;
			}
		}

		public System.Collections.ArrayList MessageHeaders
		{
			get
			{
				return this.messageHeaders;
			}
		}

		#endregion

		public string GetMessage(string method)
		{
			string returnMessage = null;

			foreach(string message in this.messageHeaders)
			{
				if (method.Length > message.Length)
					continue;
				if (method.ToLower() == message.ToLower().Substring(0, method.Length))
				{
					string message2;
					int index;

					index = message.IndexOf(':');
					if (index == -1 || index == message.Length - 1)
						continue;

					message2 = message.Substring(index + 1);

					returnMessage += message2.Trim() + ",";
				}
			}

			if (returnMessage != null)
				returnMessage = returnMessage.Substring(0, returnMessage.Length - 1);

			return returnMessage;
		}

		public void AddMessageHeader(string messageHeader)
		{
			this.messageHeaders.Add(messageHeader);
		}

		public HTTPCommand()
		{
			// 
			// TODO: コンストラクタ ロジックをここに追加してください。
			//
		}
	}
}

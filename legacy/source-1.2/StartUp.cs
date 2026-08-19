using System;
using System.Windows.Forms;
using System.IO;
using System.Runtime.Serialization.Formatters.Soap;
using TTFileArrangement;

namespace EasyHTTPServer
{
	/// <summary>
	/// StartUp の概要の説明です。
	/// </summary>
	public class StartUp
	{
		const string SaveFormHTTPServer = "SaveFormHTTPServer.xml";

		/*public StartUp()
		{
			// 
			// TODO: コンストラクタ ロジックをここに追加してください。
			//
			/// <summary>
		
		
		}*/

		[STAThread]
		static void Main() 
		{
			FormHTTPServer formHTTPServer = new FormHTTPServer();
			FormHTTPServer.FormHTTPServerSetting formHTTPServerSetting = new FormHTTPServer.FormHTTPServerSetting();

			CGI cgi1 = new CGI();
			cgi1.PATH_TRANSLATED = false;
			cgi1.ExtensionsString = "cgi,pl";
			cgi1.Enable = true;
			cgi1.ExePath = "";
			cgi1.FirstLineRead = true;

			CGI cgi2 = new CGI();
			cgi2.PATH_TRANSLATED = true;
			cgi2.ExtensionsString = "php";
			cgi2.Enable = true;
			cgi2.ExePath = @"c:\php\php.exe";
			cgi2.FirstLineRead = false;

			formHTTPServerSetting.CGICollection.Add(cgi1);
			formHTTPServerSetting.CGICollection.Add(cgi2);

			ContentType ct1 = new ContentType();
			ct1.ContentName = "application/postscript";
			ct1.Extension = "ai";
			ContentType ct2 = new ContentType();
			ct2.ContentName = "audio/aiff";
			ct2.Extension = "aif";
			ContentType ct3 = new ContentType();
			ct3.ContentName = "audio/aiff";
			ct3.Extension = "aifc";
			ContentType ct4 = new ContentType();
			ct4.ContentName = "audio/aiff";
			ct4.Extension = "aiff";
			ContentType ct5 = new ContentType();
			ct5.ContentName = "video/x-ms-asf";
			ct5.Extension = "asf";
			ContentType ct6 = new ContentType();
			ct6.ContentName = "video/x-ms-asf";
			ct6.Extension = "asx";
			ContentType ct7 = new ContentType();
			ct7.ContentName = "audio/basic";
			ct7.Extension = "au";
			ContentType ct8 = new ContentType();
			ct8.ContentName = "video/x-msvideo";
			ct8.Extension = "avi";
			ContentType ct9 = new ContentType();
			ct9.ContentName = "image/bmp";
			ct9.Extension = "bmp";
			ContentType ct10 = new ContentType();
			ct10.ContentName = "image/pjpeg";
			ct10.Extension = "jfif";
			ContentType ct11 = new ContentType();
			ct11.ContentName = "image/jpeg";
			ct11.Extension = "jpe";
			ContentType ct12 = new ContentType();
			ct12.ContentName = "image/jpeg";
			ct12.Extension = "jpe";
			ContentType ct13 = new ContentType();
			ct13.ContentName = "image/jpeg";
			ct13.Extension = "jpeg";
			ContentType ct14 = new ContentType();
			ct14.ContentName = "image/jpeg";
			ct14.Extension = "jpg";
			ContentType ct15 = new ContentType();
			ct15.ContentName = "image/gif";
			ct15.Extension = "gif";
			ContentType ct16 = new ContentType();
			ct16.ContentName = "text/html";
			ct16.Extension = "html";
			ContentType ct17 = new ContentType();
			ct17.ContentName = "text/html";
			ct17.Extension = "htm";
			ContentType ct18 = new ContentType();
			ct18.ContentName = "text/plain";
			ct18.Extension = "txt";
			ContentType ct19 = new ContentType();
			ct19.ContentName = "image/jpeg";
			ct19.Extension = "jpe";
			ContentType ct20 = new ContentType();
			ct20.ContentName = "audio/wav";
			ct20.Extension = "wav";
			ContentType ct21 = new ContentType();
			ct21.ContentName = "audio/x-ms-wma";
			ct21.Extension = "wma";
			ContentType ct22 = new ContentType();
			ct22.ContentName = "audio/mpeg";
			ct22.Extension = "mp3";
			ContentType ct23 = new ContentType();
			ct23.ContentName = "audio/x-ms-wma";
			ct23.Extension = "wma";
			ContentType ct24 = new ContentType();
			ct24.ContentName = "audio/mid";
			ct24.Extension = "mid";
			ContentType ct25 = new ContentType();
			ct25.ContentName = "audio/mid";
			ct25.Extension = "midi";
			ContentType ct26 = new ContentType();
			ct26.ContentName = "video/mpeg";
			ct26.Extension = "mp2";
			ContentType ct27 = new ContentType();
			ct27.ContentName = "video/mpeg";
			ct27.Extension = "mp2v";
			ContentType ct28 = new ContentType();
			ct28.ContentName = "video/mpeg";
			ct28.Extension = "mpa";
			ContentType ct29 = new ContentType();
			ct29.ContentName = "video/mpeg";
			ct29.Extension = "mpeg";
			ContentType ct30 = new ContentType();
			ct30.ContentName = "video/mpeg";
			ct30.Extension = "mpg";
			ContentType ct31 = new ContentType();
			ct31.ContentName = "video/mpeg";
			ct31.Extension = "mpv2";
			ContentType ct32 = new ContentType();
			ct32.ContentName = "video/x-ms-wmv";
			ct32.Extension = "wmv";
			ContentType ct33 = new ContentType();
			ct33.ContentName = "video/x-ms-wmv";
			ct33.Extension = "wmx";
			ContentType ct34 = new ContentType();
			ct34.ContentName = "video/x-msvideo";
			ct34.Extension = "avi";
		
			formHTTPServerSetting.ContentTypeCollection.Add(ct1);
			formHTTPServerSetting.ContentTypeCollection.Add(ct2);
			formHTTPServerSetting.ContentTypeCollection.Add(ct3);
			formHTTPServerSetting.ContentTypeCollection.Add(ct4);
			formHTTPServerSetting.ContentTypeCollection.Add(ct5);
			formHTTPServerSetting.ContentTypeCollection.Add(ct6);
			formHTTPServerSetting.ContentTypeCollection.Add(ct7);
			formHTTPServerSetting.ContentTypeCollection.Add(ct8);
			formHTTPServerSetting.ContentTypeCollection.Add(ct9);
			formHTTPServerSetting.ContentTypeCollection.Add(ct10);
			formHTTPServerSetting.ContentTypeCollection.Add(ct11);
			formHTTPServerSetting.ContentTypeCollection.Add(ct12);
			formHTTPServerSetting.ContentTypeCollection.Add(ct13);
			formHTTPServerSetting.ContentTypeCollection.Add(ct14);
			formHTTPServerSetting.ContentTypeCollection.Add(ct15);
			formHTTPServerSetting.ContentTypeCollection.Add(ct16);
			formHTTPServerSetting.ContentTypeCollection.Add(ct17);
			formHTTPServerSetting.ContentTypeCollection.Add(ct18);
			formHTTPServerSetting.ContentTypeCollection.Add(ct19);
			formHTTPServerSetting.ContentTypeCollection.Add(ct20);
			formHTTPServerSetting.ContentTypeCollection.Add(ct21);
			formHTTPServerSetting.ContentTypeCollection.Add(ct22);
			formHTTPServerSetting.ContentTypeCollection.Add(ct23);
			formHTTPServerSetting.ContentTypeCollection.Add(ct24);
			formHTTPServerSetting.ContentTypeCollection.Add(ct25);
			formHTTPServerSetting.ContentTypeCollection.Add(ct26);
			formHTTPServerSetting.ContentTypeCollection.Add(ct27);
			formHTTPServerSetting.ContentTypeCollection.Add(ct28);
			formHTTPServerSetting.ContentTypeCollection.Add(ct29);
			formHTTPServerSetting.ContentTypeCollection.Add(ct30);
			formHTTPServerSetting.ContentTypeCollection.Add(ct31);
			formHTTPServerSetting.ContentTypeCollection.Add(ct32);
			formHTTPServerSetting.ContentTypeCollection.Add(ct33);
			formHTTPServerSetting.ContentTypeCollection.Add(ct34);

			string loadFolderPath = Application.StartupPath + @"\Save\";

			SoapFormatter formatter = new SoapFormatter();

			try
			{
				using( Stream stream = new FileStream(loadFolderPath + SaveFormHTTPServer, FileMode.Open) )
				{
					try
					{
						formHTTPServerSetting = (FormHTTPServer.FormHTTPServerSetting)formatter.Deserialize(stream);						
					}
					catch//(System.Exception ex)
					{
						
					}
				}
			}
			catch
			{
			}
			/*
			SoapFormatter formatter = new SoapFormatter();

			using( Stream stream = new FileStream(saveFolderPath + SaveFormHTTPServer, FileMode.OpenOrCreate) )
			{
				stream.Position = stream.Length;
				//formatter.Serialize(stream, this.formHTTPServerSetting);
			}
*/
			formHTTPServer.FormHTTPServerSettingValue = formHTTPServerSetting;
			FormHTTPServer.EndFlag = true;
			formHTTPServer.Closing += new System.ComponentModel.CancelEventHandler(StartUp.Closing);
			SetMainMenu(formHTTPServer.MainMenuValue);
			Application.Run(formHTTPServer);
		}

		static public void SetMainMenu(System.Windows.Forms.MainMenu mainMenu)
		{
			foreach(System.Windows.Forms.MenuItem menuItem in mainMenu.MenuItems)
			{
				if (menuItem.Text == "MDI切り替え")
				{
					mainMenu.MenuItems.Remove(menuItem);
					break;
				}
			}

			System.Windows.Forms.MenuItem menuItem2 = new System.Windows.Forms.MenuItem();

			menuItem2.Text = "ヘルプ";
			menuItem2.Click += new System.EventHandler(ShowVersion);
			mainMenu.MenuItems.Add(menuItem2);
		}

		static public void ShowVersion(object o, System.EventArgs e)
		{
			FormVersion formVersion = new FormVersion();

			formVersion.ShowDialog();
		}

		static public void Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			string saveFolderPath = Application.StartupPath + @"\Save\";
			SoapFormatter formatter = new SoapFormatter();

			if (System.IO.Directory.Exists(saveFolderPath) == false)
			{
				System.IO.Directory.CreateDirectory(saveFolderPath);
			}

			System.IO.File.Delete(saveFolderPath + SaveFormHTTPServer);

			using( Stream stream = new FileStream(saveFolderPath + SaveFormHTTPServer, FileMode.OpenOrCreate) )
			{
				stream.Position = stream.Length;
				formatter.Serialize(stream, ((FormHTTPServer)sender).FormHTTPServerSettingValue);
			}
		}
	}
}

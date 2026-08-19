using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

//using TTFA.CommonControl;
//using TTFA.CommonClass;
using TTFileArrangement.HTTP;

namespace TTFileArrangement
{
	/// <summary>
	/// FormHTTPServer の概要の説明です。
	/// </summary>
	public class FormHTTPServer : System.Windows.Forms.Form
	{
		class FileNameEditor2 : System.Windows.Forms.Design.FileNameEditor
		{
			protected override void InitializeDialog(OpenFileDialog openFileDialog)
			{
				openFileDialog.CheckFileExists = false;
				openFileDialog.CheckPathExists = false;
				openFileDialog.Title = "IDとPASSの一覧を保存する";
				openFileDialog.Filter = "ID PASSファイル (*.pwd)|*.pwd|すべてのファイル (*.*)|*.*" ;
				openFileDialog.AddExtension = true;
				//openFileDialog.InitialDirectory = @"c:\";
				//openFileDialog.FileName = @"c:\*.pwd";
				
			}

		}

		class FileNameEditor3 : System.Windows.Forms.Design.FileNameEditor
		{
			protected override void InitializeDialog(OpenFileDialog openFileDialog)
			{
				//openFileDialog.CheckFileExists = false;
				//openFileDialog.CheckPathExists = false;
				openFileDialog.Title = "IDとPASSの一覧を読み込む";
				openFileDialog.Filter = "ID PASSファイル (*.pwd)|*.pwd|すべてのファイル (*.*)|*.*" ;
				//openFileDialog.AddExtension = true;
				//openFileDialog.InitialDirectory = @"c:\";
				//openFileDialog.FileName = @"c:\*.pwd";
				
			}

		}


		private enum ColumIndexName
		{
			ip = 0,
			fileName = 1,
			sendSpeed = 2,
			remainingTime = 3,
			pos = 4,
			range = 5
		}

		#region クラス

		public class TcpClient2:  System.Net.Sockets.TcpClient
		{
			public Socket Socket
			{
				set
				{
					Client = value;
				}
				get
				{
					return Client;
				}
			}
		}


		[Serializable()]
		public class FormHTTPServerSetting : ICloneable
		{
			#region フィールド

			private bool logSaveFlag = false;
			private int maxConnectNum = 50;
			private ContentTypeCollection contentTypeCollection = new ContentTypeCollection();
			private CGICollection cgiCollection = new CGICollection();
			private bool cgiRun = false;
			private bool basicAttestation = false;
			private PersonCollection personCollection = new PersonCollection();
			private int listViewUpdateTimeinterval = 1000;
			private string [] firstHTML;
			private string [] lastHTML;
			private string rootFileName = "index.html";
			private int portNumber = 80;
			private string [] folderPaths = new string[0];
			private System.Drawing.Point location;
			private Size windowSize = new Size(400, 350);
			private bool mdiFlag = true;
			
			#endregion

			#region プロパティ

			[TypeConverter(typeof(ContentTypeCollectionConverter))]
			[
			Category("ContentType"), 
			Description("ContentType")
			] 
			public ContentTypeCollection ContentTypeCollection
			{
				get
				{
					return this.contentTypeCollection;
				}
			}

			
			[
			Category("CGI"), 
			Description("CGIの設定")
			] 
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

			[
			Category("CGI"), 
			Description("CGIを実行するにはこのプロパティをtrueにしてください。")
			] 
			public bool CGIRun
			{
				get
				{
					return this.cgiRun;
				}
				set
				{
					this.cgiRun = value;
				}
			}

			[
			Category("認証"), 
			Description("最大接続数")
			] 
			public int MaxConnectNum
			{
				set
				{
					this.maxConnectNum = value;
				}
				get
				{
					return this.maxConnectNum;
				}
			}


			[
			Category("認証"), 
			Description("基本認証の有効無効。trueにするときはPersonCollectionを変更してください。")
			] 
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


			[TypeConverter(typeof(PersonCollectionConverter))]
			[
			Category("認証"), 
			Description("IDとPASSの設定")
			] 
			public PersonCollection PersonCollection
			{
				get
				{
					return this.personCollection;
				}
			}

			[
			Category("認証"), 
			Description("IDとPASSの一覧の保存"),
			Editor(typeof(FileNameEditor2), typeof(System.Drawing.Design.UITypeEditor))
			] 
			public string IDPASSExport
			{
				get
				{
					return "";
				}
				set
				{
					IDPASSExportFunc(value);
				}
			}

			[
			Category("認証"), 
			Description("IDとPASSの一覧の読み込み"),
			Editor(typeof(FileNameEditor3), typeof(System.Drawing.Design.UITypeEditor))
			] 
			public string IDPASSImport
			{
				get
				{
					return "";
				}
				set
				{
					IDPASSImportFunc(value);
				}
			}


			[ 
			Category("状態"), 
			Description("画面更新間隔(ミリ秒)")
			] 
			public int ListViewUpdateTimeinterval
			{
				set
				{
					this.listViewUpdateTimeinterval = value;
				}
				get
				{
					return this.listViewUpdateTimeinterval;
				}
			}

			[ 
			Category("サーバー設定"), 
			Description("最後のHTML")
			] 
			public string[] LastHTML
			{
				set
				{
					this.lastHTML = value;
				}
				get
				{
					return this.lastHTML;
				}
			}

			[ 
			Category("サーバー設定"), 
			Description("最初のHTML")
			] 
			public string[] FirstHTML
			{
				set
				{
					this.firstHTML = value;
				}
				get
				{
					return this.firstHTML;
				}
			}


			[ 
			Category("サーバー設定"), 
			Description("ルートファイル名")
			] 
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

			[ 
			Category("サーバー設定"), 
			Description("ポートナンバー")
			] 
			public int PortNumber
			{
				set
				{
					this.portNumber = value;
				}
				get
				{
					return this.portNumber;
				}
			}


			[ 
			Category("配置"), 
			Description("ウインドウのサイズ")
			] 
			public Size WindowSize
			{
				set
				{
					this.windowSize = value;
				}
				get
				{
					return this.windowSize;
				}
			}

			[ 
			Category("配置"), 
			Description("表示位置")
			] 
			public System.Drawing.Point Location
			{
				set
				{
					this.location = value;
				}
				get
				{
					return this.location;
				}
			}

			[ 
			Category("配置"), 
			Description("MDI設定")
			] 
			public bool MdiFlag
			{
				set
				{
					this.mdiFlag = value;
				}
				get
				{
					return this.mdiFlag;
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
					return this.folderPaths;
				}
			}

			[
			Category("ログ"), 
			Description("ログを保存するかしないか")
			] 
			public bool LogSaveFlag
			{
				set
				{
					this.logSaveFlag = value;
				}
				get
				{
					return this.logSaveFlag;
				}
			}

			#endregion

			#region メソッド

			public FormHTTPServerSetting()
			{
				this.firstHTML = new string[29];
				this.firstHTML[0] = @"<html>";
				this.firstHTML[1] = @"<meta http-equiv=""Content-type"" content=""text/html; charset=Shift_JIS"">";
				this.firstHTML[2] = @"<head>";
				this.firstHTML[3] = @"<title>TTFA HTTP Server</title>";
				this.firstHTML[4] = @"<style type=""text/css"">";
				this.firstHTML[5] = @"<!--";
				this.firstHTML[6] = @"a";
				this.firstHTML[7] = @"{";
				this.firstHTML[8] = "";//@"text-decoration: none;";
				this.firstHTML[9] = @"}";
				this.firstHTML[10] = @".folder";
				this.firstHTML[11] = @"{";
				this.firstHTML[12] = @"	font-weight: bold; font-size: 14px; color: #ffffff; background: mediumblue; padding: 2px 2px 2px 10px; margin: 2px;";
				this.firstHTML[13] = @"}";
				this.firstHTML[14] = @".file";
				this.firstHTML[15] = @"{";
				this.firstHTML[16] = @"   font-weight: bold; font-size: 16px; color: #000000 ;text-decoration: none;";
				this.firstHTML[17] = @"}";
				this.firstHTML[18] = @".filesize";
				this.firstHTML[19] = @"{";
				this.firstHTML[20] = @"   font-weight: bold; font-size: 12px; color: navy ;";
				this.firstHTML[21] = @"}";
				this.firstHTML[22] = @"-->";
				this.firstHTML[23] = @"</style>";
				this.firstHTML[24] = @"</head>";
				this.firstHTML[25] = @"<body bgcolor=""deepskyblue"" LINK=""#FFFFFF"" VLINK =""yellow"">";
				this.firstHTML[26] = @"<div style="" margin: 5px 5px 5px 5px; font-weight: bold; color: #ffffff; font-size: 20px; width: 300px; filter:Shadow(color=blue)"";>";
				this.firstHTML[27] = @"TTFA HTTP Server";
				this.firstHTML[28] = @"</div>";


				

				
				this.lastHTML = new string[2];
				this.lastHTML[0] = @"</body>";
				this.lastHTML[1] = @"</html>";
				
			}

			public object Clone()
			{
				FormHTTPServerSetting formHTTPServerSetting = new FormHTTPServerSetting();
	
				if (this.folderPaths != null)
					formHTTPServerSetting.folderPaths = (string[])this.folderPaths.Clone();

				formHTTPServerSetting.Location = this.Location;
				formHTTPServerSetting.WindowSize = this.windowSize;
				formHTTPServerSetting.MdiFlag = this.MdiFlag;
				formHTTPServerSetting.RootFileName = this.RootFileName;
				formHTTPServerSetting.PortNumber = this.PortNumber;
				formHTTPServerSetting.FirstHTML = this.FirstHTML;
				formHTTPServerSetting.LastHTML = this.LastHTML;
				formHTTPServerSetting.listViewUpdateTimeinterval = this.listViewUpdateTimeinterval;
				formHTTPServerSetting.personCollection = (PersonCollection)this.personCollection.Clone();
				formHTTPServerSetting.basicAttestation = this.basicAttestation;
				formHTTPServerSetting.cgiRun = this.cgiRun;
				formHTTPServerSetting.CGICollection = (CGICollection)this.CGICollection.Clone();
				formHTTPServerSetting.contentTypeCollection = (ContentTypeCollection)this.contentTypeCollection.Clone();
				formHTTPServerSetting.maxConnectNum = this.maxConnectNum;
				formHTTPServerSetting.logSaveFlag = this.logSaveFlag;

				return formHTTPServerSetting;
			}

			private void IDPASSExportFunc(string filePath)
			{
				System.IO.StreamWriter writer = null;
				try
				{
					if (System.Windows.Forms.MessageBox.Show("保存しますか？") == System.Windows.Forms.DialogResult.OK)
					{
						System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
						if (fileInfo.Exists == true)
							fileInfo.Delete();
						writer = new System.IO.StreamWriter(filePath, true);
						foreach(Person person in this.personCollection)
						{
							writer.WriteLine(person.ID + ":" + person.Pass);
						}
					}
					writer.Close();
				}
				catch
				{
					if (writer != null)
						writer.Close();
				}
			}

			private void IDPASSImportFunc(string filePath)
			{
				System.IO.StreamReader reader = null;
				try
				{
					if (System.Windows.Forms.MessageBox.Show("読み込みますか？") == System.Windows.Forms.DialogResult.OK)
					{
						System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
						reader = new System.IO.StreamReader(filePath);
						PersonCollection personCollection2 = new PersonCollection();

						string idPassLine;
						do
						{
							idPassLine = reader.ReadLine();
							if (idPassLine == null)
								break;
							string[] idPass = idPassLine.Split(new char[]{':'});
							if (idPass.Length != 2)
								continue;
							string id = idPass[0];
							string pass = idPass[1];
							Person person = new Person();
							person.ID = id;
							person.Pass = pass;
							personCollection2.Add(person);
						}while(true);
						this.personCollection = personCollection2;
					}
					reader.Close();
					System.Windows.Forms.MessageBox.Show("読み込みに成功しました");
				}
				catch
				{
					if (reader != null)
						reader.Close();
					System.Windows.Forms.MessageBox.Show("読み込みに失敗しました");
				}
			}


			#endregion
		}

		#endregion

		#region フィールド

		public static bool EndFlag = false;
		public const int DownloadSizesMaxNum = 5;
		private Form tmpMdiParentForm;
		private TcpListener listener;
		private const string listenStartString = "待ち受け停止します";
		private const string listenWaitString = "待ち受け開始します";
		private System.Collections.Hashtable threads = new System.Collections.Hashtable();
		private const string saveLogPath = @"httplog.log";
		static private System.IO.StreamWriter writer;
		private Thread listenThread;
		private FormHTTPServerSetting formHTTPServerSetting;
		private bool closingFlag = false;

		#endregion 

		#region プロパティ

		public System.Windows.Forms.MainMenu MainMenuValue
		{
			get
			{
				return this.mainMenu1;
			}
		}

		public Form TmpMdiParentForm
		{
			set
			{
				this.tmpMdiParentForm = value;
			}
			get
			{
				return this.tmpMdiParentForm;
			}
		}


		public FormHTTPServerSetting FormHTTPServerSettingValue
		{
			set
			{
				this.formHTTPServerSetting = value;
				this.MDIChange();
				this.timer1.Interval = value.ListViewUpdateTimeinterval;
				this.textBox1.Text = value.PortNumber.ToString();
				this.listBox1.Items.Clear();
				foreach(string folderPath in this.formHTTPServerSetting.FolderPaths)
				{
					this.listBox1.Items.Add(folderPath);
				}
			}
			get
			{
				this.formHTTPServerSetting.WindowSize = this.Size;
				this.formHTTPServerSetting.Location = this.Location;
				return this.formHTTPServerSetting;
			}
			
		}
		

		#endregion

		
		private System.Windows.Forms.ListBox listBox1;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.Button button3;
		private System.Windows.Forms.MainMenu mainMenu1;
		private System.Windows.Forms.MenuItem menuItem1;
		private System.Windows.Forms.ListView listView1;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.TextBox textBox2;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Splitter splitter1;
		private System.Windows.Forms.Button button4;
		private System.Windows.Forms.TextBox textBox3;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.OpenFileDialog openFileDialog1;
		private System.Windows.Forms.ColumnHeader columnHeader2;
		private System.Windows.Forms.ColumnHeader columnHeader3;
		private System.Windows.Forms.ColumnHeader columnHeader4;
		private System.Windows.Forms.ColumnHeader columnHeader5;
		private System.Windows.Forms.ColumnHeader columnHeader6;
		private System.Windows.Forms.Timer timer1;
		private System.Windows.Forms.MenuItem menuItem2;
        private FolderBrowserDialog folderBrowserDialog1;
		private System.ComponentModel.IContainer components;

		public FormHTTPServer()
		{
			//
			// Windows フォーム デザイナ サポートに必要です。
			//
			InitializeComponent();

			//
			// TODO: InitializeComponent 呼び出しの後に、コンストラクタ コードを追加してください。
			//
		}

		/// <summary>
		/// 使用されているリソースに後処理を実行します。
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormHTTPServer));
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.mainMenu1 = new System.Windows.Forms.MainMenu(this.components);
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader2 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader3 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader4 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader5 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader6 = new System.Windows.Forms.ColumnHeader();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.button4 = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.ItemHeight = 12;
            this.listBox1.Location = new System.Drawing.Point(0, 88);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(208, 52);
            this.listBox1.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(0, 8);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(288, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "待ちうけ開始";
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(0, 56);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 19);
            this.textBox1.TabIndex = 2;
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // label1
            // 
            this.label1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label1.Location = new System.Drawing.Point(0, 40);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(100, 24);
            this.label1.TabIndex = 3;
            this.label1.Text = "Port";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(216, 88);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 4;
            this.button2.Text = "追加";
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(216, 120);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 5;
            this.button3.Text = "削除";
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // mainMenu1
            // 
            this.mainMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem2,
            this.menuItem1});
            // 
            // menuItem2
            // 
            this.menuItem2.Index = 0;
            this.menuItem2.Text = "MDI切り替え";
            this.menuItem2.Click += new System.EventHandler(this.menuItem2_Click);
            // 
            // menuItem1
            // 
            this.menuItem1.Index = 1;
            this.menuItem1.Text = "個別設定";
            this.menuItem1.Click += new System.EventHandler(this.menuItem1_Click);
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5,
            this.columnHeader6});
            this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView1.Location = new System.Drawing.Point(0, 195);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(504, 126);
            this.listView1.TabIndex = 6;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "接続先IP";
            this.columnHeader1.Width = 120;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "ファイル名";
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "転送速度";
            this.columnHeader3.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "残り時間";
            this.columnHeader4.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "位置";
            this.columnHeader5.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnHeader5.Width = 100;
            // 
            // columnHeader6
            // 
            this.columnHeader6.Text = "範囲";
            this.columnHeader6.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnHeader6.Width = 100;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(104, 56);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(100, 19);
            this.textBox2.TabIndex = 7;
            this.textBox2.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            // 
            // label2
            // 
            this.label2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label2.Location = new System.Drawing.Point(104, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(100, 32);
            this.label2.TabIndex = 8;
            this.label2.Text = "RootFileName";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.textBox3);
            this.panel1.Controls.Add(this.button4);
            this.panel1.Controls.Add(this.textBox1);
            this.panel1.Controls.Add(this.listBox1);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.textBox2);
            this.panel1.Controls.Add(this.button3);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(504, 192);
            this.panel1.TabIndex = 9;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint);
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(0, 168);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(304, 19);
            this.textBox3.TabIndex = 10;
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(312, 144);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(75, 48);
            this.button4.TabIndex = 9;
            this.button4.Text = "URL取得してクリップボードにコピー";
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // label3
            // 
            this.label3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label3.Location = new System.Drawing.Point(0, 152);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(304, 32);
            this.label3.TabIndex = 11;
            this.label3.Text = "URL";
            // 
            // splitter1
            // 
            this.splitter1.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitter1.Location = new System.Drawing.Point(0, 192);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(504, 3);
            this.splitter1.TabIndex = 10;
            this.splitter1.TabStop = false;
            // 
            // timer1
            // 
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // FormHTTPServer
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(504, 321);
            this.Controls.Add(this.listView1);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Menu = this.mainMenu1;
            this.Name = "FormHTTPServer";
            this.Text = "HTTPServer画面 停止中";
            this.Closed += new System.EventHandler(this.FormHTTPServer_Closed);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.FormHTTPServer_Closing);
            this.Load += new System.EventHandler(this.FormHTTPServer_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

		}
		#endregion

		#region イベント

		private void SettingOkPushed(object sender, System.EventArgs arg)
		{
			this.FormHTTPServerSettingValue = (FormHTTPServerSetting)(((FormCommonSetting)sender).SelectedObject);
			
			((FormCommonSetting)(sender)).Close();
		}
		private void SettingCancelPushed(object sender, System.EventArgs arg)
		{
			((FormCommonSetting)(sender)).Close();
		}

		private void LogSave(object sender, System.EventArgs e)
		{
			if (this.formHTTPServerSetting.LogSaveFlag == false)
			{
				return;
			}

			lock(typeof(FormHTTPServer))
			{
				if (FormHTTPServer.writer == null)
				{
					string saveFolderPath = Application.StartupPath + @"\log\";
					System.IO.Stream stream = new System.IO.FileStream(saveFolderPath + FormHTTPServer.saveLogPath, System.IO.FileMode.OpenOrCreate);
					stream.Position = stream.Length;
					FormHTTPServer.writer = new System.IO.StreamWriter(stream, System.Text.Encoding.GetEncoding(932));
					
				}

			
				FormHTTPServer.writer.Write((string)sender);
			}	
		}


		/*
		public delegate void MyDelegate(object sender, System.EventArgs e);

		public void SocketeClosed(object sender, System.EventArgs e)
		{
			object [] args = { sender, System.EventArgs.Empty };

			//MyDelegate eventHandler1 = new MyDelegate(this.SocketeClosed2);
			System.EventHandler eventHandler2 = new System.EventHandler(this.SocketeClosed2);
			IAsyncResult ar = this.BeginInvoke(eventHandler2 , args);

			// シグナルを待つ。
			ar.AsyncWaitHandle.WaitOne();
		}
		*/

		public void SocketClosed(object sender, System.EventArgs e)
		{
			if (this.InvokeRequired)
			{
				this.Invoke(new HTTPServer.NormalDelegate(this.SocketClosed), new object[] {sender, e});
				return;
			}
			
			HTTPServer httpServer = (HTTPServer)sender;
			ListViewItem item = (ListViewItem)this.threads[httpServer];
			if (item != null)
			{
				item.Remove();
				this.threads.Remove(sender);
			}
		}


		#endregion

		#region 待ち受けスタート

		private void ListenStart()
		{
			try
			{
				//this.timer1.Enabled = true;
				this.listener = 
					new TcpListener( IPAddress.Any, this.formHTTPServerSetting.PortNumber);

				// 接続要求受け入れ開始
				this.listener.Start();

				this.SetButton1(FormHTTPServer.listenStartString, System.EventArgs.Empty);
				this.SetTitle("HTTPServer 画面 実行中", System.EventArgs.Empty);	

				while( true)
				{
					
					TcpClient2 tcpClient2 = new TcpClient2();
					tcpClient2.Socket = listener.AcceptSocket();
					if (this.threads.Count >= this.FormHTTPServerSettingValue.MaxConnectNum)
					{
						tcpClient2.Socket.Close();
						continue;
					}
					HTTPServer httpServer = new HTTPServer(this);
					httpServer.ContentTypeCollection = this.formHTTPServerSetting.ContentTypeCollection;
					httpServer.FolderPaths = this.formHTTPServerSetting.FolderPaths;
					httpServer.TcpClient = tcpClient2;
					httpServer.RequestGeted += new HTTPServer.NormalDelegate(this.LogSave);
					httpServer.ResponseSended += new HTTPServer.NormalDelegate(this.LogSave);
					httpServer.SocketClosed += new HTTPServer.NormalDelegate(this.SocketClosed);
					httpServer.RootFileName = this.formHTTPServerSetting.RootFileName;
					httpServer.FirstHTML = this.formHTTPServerSetting.FirstHTML;
					httpServer.LastHTML = this.formHTTPServerSetting.LastHTML;
					httpServer.BasicAttestation = this.formHTTPServerSetting.BasicAttestation;
					httpServer.HashIDPASS = this.MakeHashIDPASS();
					httpServer.CGIRun = this.formHTTPServerSetting.CGIRun;
					httpServer.CGICollection = this.formHTTPServerSetting.CGICollection;
					httpServer.IPAddress = ((IPEndPoint)tcpClient2.Socket.RemoteEndPoint).Address.ToString()
						+ ":"
						+ ((IPEndPoint)tcpClient2.Socket.RemoteEndPoint).Port.ToString();
                    this.Invoke(new System.EventHandler(this.ADDListItem), new object[] { httpServer, System.EventArgs.Empty });


                    this.threads.Add(httpServer, httpServer.Tag);
					Thread thread = new Thread(new ThreadStart(httpServer.ListenStart));
					thread.Start();
				}
			}
			catch(System.Net.Sockets.SocketException e)
			{
				this.ThreadsEnd(null, System.EventArgs.Empty);
				if (e.ErrorCode != 10004)
				{
					System.Windows.Forms.MessageBox.Show(e.Message);
				}
				if (this.closingFlag == true)
					return;
				this.SetButton1(FormHTTPServer.listenWaitString, System.EventArgs.Empty);
				this.SetTitle("HTTPServer画面 停止中", System.EventArgs.Empty);
				
				return;
			}
			catch(System.Exception e)
			{
                this.ThreadsEnd(null, System.EventArgs.Empty); ;
				this.SetButton1(FormHTTPServer.listenWaitString, System.EventArgs.Empty);
				this.SetTitle("HTTPServer画面 停止中", System.EventArgs.Empty);
				System.Windows.Forms.MessageBox.Show(e.Message);
				
			}
		}

        private void ADDListItem(object sender, System.EventArgs e)
        {
            //if (this.InvokeRequired)
            //{
                
                //return;
            //}
            HTTPServer httpServer = sender as HTTPServer;
            ListViewItem item = this.listView1.Items.Add("");
            item.SubItems.Add("");
            item.SubItems.Add("");
            item.SubItems.Add("");
            item.SubItems.Add("");
            item.SubItems.Add("");
            httpServer.Tag = item;
            item.Text = httpServer.IPAddress;
        }

		#endregion

		#region その他メソッド

		private System.Collections.Hashtable MakeHashIDPASS()
		{
			System.Collections.Hashtable hashIDPASS = new System.Collections.Hashtable();

			foreach(Person person in this.formHTTPServerSetting.PersonCollection)
			{
				try
				{
					hashIDPASS.Add(person.ID, person.Pass);
				}
				catch
				{
					continue;
				}
			}

			return hashIDPASS;
		}

        private void ThreadsEnd(object sender, System.EventArgs e)
		{
            if (this.InvokeRequired)
            {
                this.Invoke(new System.EventHandler(this.ThreadsEnd), new object[] { sender, System.EventArgs.Empty });
                return;
            }

			System.Collections.ArrayList httpServers = new System.Collections.ArrayList();
			foreach(System.Collections.DictionaryEntry dictionaryEntry in this.threads)
			{
				httpServers.Add(((HTTPServer)(dictionaryEntry.Key)));
			}
			foreach(HTTPServer httpServer in httpServers)
			{
				httpServer.SocketClosed -= new HTTPServer.NormalDelegate(this.SocketClosed);
				httpServer.Close();
			}
			if (this.closingFlag == false)
			{
				this.listView1.Items.Clear();
			}
		}
		
		#endregion

		#region MDI

		private void MDIChange()
		{
			if (this.formHTTPServerSetting.MdiFlag == true)
			{
				this.MDIIN();
			}
			else
			{
				this.MDIOUT();
			}
		}

		private void MDIIN()
		{
			this.formHTTPServerSetting.MdiFlag = true;
			this.MdiParent = this.TmpMdiParentForm;
		}

		private void MDIOUT()
		{
			this.formHTTPServerSetting.MdiFlag = false;
			this.MdiParent = null;
		}


		#endregion

		#region ListBox
		private void FolderPathsUpDate()
		{
			this.formHTTPServerSetting.FolderPaths = new string[this.listBox1.Items.Count];
			for(int index = 0; index < this.listBox1.Items.Count; index++)
			{
				this.formHTTPServerSetting.FolderPaths[index] = this.listBox1.Items[index].ToString();
			}
		}
		#endregion

		#region 表示メソッド

		private void SetButton1(object sender, System.EventArgs e)
		{
			if (this.InvokeRequired)
			{
				Invoke(new HTTPServer.NormalDelegate(this.SetButton1),new object[] { sender, e });
				return;
			}

			this.button1.Text = (string)sender;
		}

		private void SetTitle(object sender, System.EventArgs e)
		{
			if (this.InvokeRequired)
			{
				Invoke(new HTTPServer.NormalDelegate(this.SetTitle),new object[] { sender, e });
				return;
			}

			this.Text = (string)sender;
		}
		#endregion

		#region IP取得

		private string GetIP(string URL)
		{
			System.Net.WebClient webClient = new System.Net.WebClient();
		
			//streamを開く
			System.IO.Stream stream = webClient.OpenRead(URL);
			//読み込む
			System.IO.StreamReader streamReader = new System.IO.StreamReader(stream);

			return streamReader.ReadLine();

		}

		#endregion

		private void button1_Click(object sender, System.EventArgs e)
		{
			if (this.listBox1.Items.Count == 0)
			{
				System.Windows.Forms.MessageBox.Show("公開するフォルダを追加してください");
				return;
			}
			if (button1.Text == FormHTTPServer.listenWaitString)
			{
				this.timer1.Enabled = true;
				listenThread = new Thread(new ThreadStart(ListenStart));
				listenThread.Start();
			}
			else
			{
				this.timer1.Enabled = false;
				this.listener.Stop();
			}
			
		}

		private void menuItem1_Click(object sender, System.EventArgs e)
		{
			FormCommonSetting formCommonSetting = new FormCommonSetting();
			FormHTTPServerSetting formHTTPServerSetting = (FormHTTPServerSetting)this.FormHTTPServerSettingValue.Clone();
			formCommonSetting.SelectedObject = formHTTPServerSetting;
			formCommonSetting.OkPushed += new System.EventHandler(this.SettingOkPushed);
			formCommonSetting.CancelPushed += new System.EventHandler(this.SettingCancelPushed);
			formCommonSetting.Show();
		}

		private void button2_Click(object sender, System.EventArgs e)
		{
			

            if (this.folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                int index;
                string path1;
                string path2 = null;

                for (index = 0; index < this.listBox1.Items.Count; index++)
                {
                    path1 = this.listBox1.Items[index].ToString();
                    path2 = this.folderBrowserDialog1.SelectedPath;
                    if (path1 == path2)
                    {
                        System.Windows.Forms.MessageBox.Show("パスが重複しています");
                        break;
                    }
                    if (path1[path1.Length - 1] != '\\')
                    {
                        if (System.IO.Path.GetFileName(path1) == System.IO.Path.GetFileName(path2))
                        {
                            System.Windows.Forms.MessageBox.Show("同じディレクトリ名を含めることはできません");
                            break;
                        }
                    }
                }
                if (index >= this.listBox1.Items.Count)
                {
                    this.listBox1.Items.Add(this.folderBrowserDialog1.SelectedPath);
                }
                FolderPathsUpDate();
            }
		
		
			
		}

		private void FormHTTPServer_Closed(object sender, System.EventArgs e)
		{
			
		}

		private void FormHTTPServer_Load(object sender, System.EventArgs e)
		{
			this.textBox1.Text = this.formHTTPServerSetting.PortNumber.ToString();
			this.textBox2.Text = this.formHTTPServerSetting.RootFileName;
			this.button1.Text = FormHTTPServer.listenWaitString;

			if (FormHTTPServer.writer == null)
			{
				string saveFolderPath = Application.StartupPath + @"\log\";

				if (System.IO.Directory.Exists(saveFolderPath) == false)
				{
					System.IO.Directory.CreateDirectory(saveFolderPath);
				}
			}
		}

		private void textBox1_TextChanged(object sender, System.EventArgs e)
		{
			try
			{
				this.formHTTPServerSetting.PortNumber = int.Parse(this.textBox1.Text);
			}
			catch
			{
				this.textBox1.Text = this.formHTTPServerSetting.PortNumber.ToString();
			}
		}

		private void button3_Click(object sender, System.EventArgs e)
		{
			if (this.listBox1.SelectedIndex != -1)
			{
				this.listBox1.Items.RemoveAt(this.listBox1.SelectedIndex);
			}
			FolderPathsUpDate();
		}

		private void textBox2_TextChanged(object sender, System.EventArgs e)
		{
			this.formHTTPServerSetting.RootFileName = textBox2.Text;			
		}

		private void FormHTTPServer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (FormHTTPServer.EndFlag == false)
			{
				if(System.Windows.Forms.MessageBox.Show("個別設定が失われますが閉じますか？", "終了", System.Windows.Forms.MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.Cancel)
				{
					e.Cancel = true;
					return;
				}
			}

			if (FormHTTPServer.writer != null)
			{
				FormHTTPServer.writer.Close();
				FormHTTPServer.writer = null;
			}
			if (this.listener != null)
			{
				this.closingFlag = true;
				this.listener.Stop();
			}
			//this.ThreadsEnd();
			System.Threading.Thread.Sleep(1000);
		}

		private void button4_Click(object sender, System.EventArgs e)
		{
			string ip = this.GetIP(@"http://unyora.sakura.ne.jp/GetIP.cgi");
			this.textBox3.Text = "http://" + ip + ":" + this.textBox1.Text + "/" + this.textBox2.Text;
			Clipboard.SetDataObject(this.textBox3.Text, false);
		}

		private void timer1_Tick(object sender, System.EventArgs e)
		{
			if (this.InvokeRequired)
			{
				//int z = 500;
			}
			

			this.listView1.BeginUpdate();

			foreach(System.Collections.DictionaryEntry dictionaryEntry in this.threads)
			{
				HTTPServer httpServer = ((HTTPServer)(dictionaryEntry.Key));
				ListViewItem listViewItem = (ListViewItem)httpServer.Tag;
				FileSendStatus fileSendStatus = httpServer.FileSendStatusProperty;
				long downloadedFileSizedifference;
				long filePos;
				long downloadSpeed;
				System.TimeSpan timeSpan;

				if (fileSendStatus == null)
				{
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.ip].Text = httpServer.IPAddress;
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.fileName].Text = "";
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.pos].Text = "";
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.range].Text = "";
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.remainingTime].Text = "";
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.sendSpeed].Text = "";
					continue;
				}

				System.Collections.Queue queue = fileSendStatus.Tag as System.Collections.Queue;

				if (queue == null)
				{
					queue = new System.Collections.Queue();
					fileSendStatus.Tag = queue;
					queue.Enqueue(fileSendStatus.FilePosStart);
				}
				filePos = fileSendStatus.FilePos;
				queue.Enqueue(filePos);
				if (queue.Count < 2)
					continue;
				while(queue.Count > FormHTTPServer.DownloadSizesMaxNum)
					queue.Dequeue();
				downloadedFileSizedifference = filePos - (long)queue.Peek();
				downloadSpeed = (int)((double)downloadedFileSizedifference * this.formHTTPServerSetting.ListViewUpdateTimeinterval / 1000 / (queue.Count - 1));
				if (downloadSpeed != 0)
				{
					timeSpan = new System.TimeSpan(0 ,0 , (int)((fileSendStatus.FilePosEnd - fileSendStatus.FilePos) / downloadSpeed));
					listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.remainingTime].Text = ((int)(timeSpan.Days * 24 + timeSpan.Hours)).ToString() + "時間"
						+ timeSpan.Minutes + "分" + timeSpan.Seconds + "秒";
				}

				listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.sendSpeed].Text = ((int)(downloadSpeed / 1024)).ToString() + "KB/S";
				
				listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.pos].Text = String.Format("{0:N0}", fileSendStatus.FilePos);
				listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.range].Text 
					= String.Format("{0:N0}", fileSendStatus.FilePosStart)
					+ "-"
					+ String.Format("{0:N0}", fileSendStatus.FilePosEnd);
				listViewItem.SubItems[(int)FormHTTPServer.ColumIndexName.fileName].Text = fileSendStatus.FileName;
			}

			this.listView1.EndUpdate();
			
		
		}

		private void menuItem2_Click(object sender, System.EventArgs e)
		{
			this.formHTTPServerSetting.MdiFlag =
				this.formHTTPServerSetting.MdiFlag == true ?
				false : true;
			
			MDIChange();
		}

		private void panel1_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
		{
		
		}
	}
}

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace TTFileArrangement
{
	/// <summary>
	/// FormFileFolderSetting の概要の説明です。
	/// </summary>
	public class FormCommonSetting : System.Windows.Forms.Form
	{
		#region フィールド
		public event System.EventHandler OkPushed;
		public event System.EventHandler CancelPushed;
		#endregion
		/// <summary>
		/// 必要なデザイナ変数です。
		/// </summary>
		private System.ComponentModel.Container components = null;
		private System.Windows.Forms.Button button2;
		public System.Windows.Forms.PropertyGrid OptionsPropertyGrid;
		private System.Windows.Forms.Button button3;

		#region プロパティ

		public object SelectedObject
		{
			set
			{
				OptionsPropertyGrid.SelectedObject = value;
				
			}
			get
			{
				return OptionsPropertyGrid.SelectedObject;
			}
		}

		#endregion
		public FormCommonSetting()
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
			this.button2 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.OptionsPropertyGrid = new System.Windows.Forms.PropertyGrid();
			this.SuspendLayout();
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point(8, 304);
			this.button2.Name = "button2";
			this.button2.TabIndex = 0;
			this.button2.Text = "ok";
			this.button2.Click += new System.EventHandler(this.button2_Click);
			// 
			// button3
			// 
			this.button3.Location = new System.Drawing.Point(336, 304);
			this.button3.Name = "button3";
			this.button3.TabIndex = 3;
			this.button3.Text = "キャンセル";
			this.button3.Click += new System.EventHandler(this.button3_Click);
			// 
			// OptionsPropertyGrid
			// 
			this.OptionsPropertyGrid.CommandsVisibleIfAvailable = true;
			this.OptionsPropertyGrid.Dock = System.Windows.Forms.DockStyle.Top;
			this.OptionsPropertyGrid.LargeButtons = false;
			this.OptionsPropertyGrid.LineColor = System.Drawing.SystemColors.ScrollBar;
			this.OptionsPropertyGrid.Name = "OptionsPropertyGrid";
			this.OptionsPropertyGrid.Size = new System.Drawing.Size(424, 288);
			this.OptionsPropertyGrid.TabIndex = 1;
			this.OptionsPropertyGrid.Text = "PropertyGrid";
			this.OptionsPropertyGrid.ViewBackColor = System.Drawing.SystemColors.Window;
			this.OptionsPropertyGrid.ViewForeColor = System.Drawing.SystemColors.WindowText;
			// 
			// FormCommonSetting
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
			this.ClientSize = new System.Drawing.Size(424, 341);
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.button2,
																		  this.button3,
																		  this.OptionsPropertyGrid});
			this.Name = "FormCommonSetting";
			this.Resize += new System.EventHandler(this.FormCommonSetting_Resize);
			this.ResumeLayout(false);

		}
		#endregion

		#region コントロールイベント

		private void button2_Click(object sender, System.EventArgs e)
		{
			if (this.OkPushed != null)
			{
				this.OkPushed(this, System.EventArgs.Empty);
			}
		}

		private void button3_Click(object sender, System.EventArgs e)
		{
			if (this.CancelPushed != null)
			{
				this.CancelPushed(this, System.EventArgs.Empty);
			}
		
		}

		#endregion

		private void FormCommonSetting_Resize(object sender, System.EventArgs e)
		{
			this.OptionsPropertyGrid.Height = this.Size.Height - 70;
			this.button2.Top = this.Size.Height - 50;
			this.button3.Top = this.Size.Height - 50;
			this.button3.Left = this.Size.Width - 100;
			//this.OptionsPropertyGrid.Height = this.Height - 100;
			//this.button2.Left = this.OptionsPropertyGrid.Left + 200;
		}
	}
}

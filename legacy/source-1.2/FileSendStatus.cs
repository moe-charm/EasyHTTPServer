using System;

namespace TTFileArrangement
{
	/// <summary>
	/// FileSendStatus の概要の説明です。
	/// </summary>
	public class FileSendStatus
	{
		#region フィールド

		private long filePosStart = 0;
		private long filePosEnd = 0;
		private string fileName = "";
		private long filePos = 0;
		private object tag = null;

		#endregion

		#region プロパティ

		public long FilePosStart
		{
			
			set
			{
				lock(this)
				{
					this.filePosStart = value;
				}
			}
			get
			{
				lock(this)
				{
					return this.filePosStart;
				}
			}
		}

		public long FilePosEnd
		{
			
			set
			{
				lock(this)
				{
					this.filePosEnd = value;
				}
			}
			get
			{
				lock(this)
				{
					return this.filePosEnd;
				}
			}
		}

		public string FileName
		{
			
			set
			{
				lock(this)
				{
					this.fileName = value;
				}
			}
			get
			{
				lock(this)
				{
					return this.fileName;
				}
			}
		}

		public long FilePos
		{
			
			set
			{
				lock(this)
				{
					this.filePos = value;
				}
			}
			get
			{
				lock(this)
				{
					return this.filePos;
				}
			}
		}

		public object Tag
		{
			
			set
			{
				lock(this)
				{
					this.tag = value;
				}
			}
			get
			{
				lock(this)
				{
					return this.tag;
				}
			}
		}





		#endregion

		public FileSendStatus()
		{
			// 
			// TODO: コンストラクタ ロジックをここに追加してください。
			//
		}
	}
}

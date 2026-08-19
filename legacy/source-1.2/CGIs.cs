using System;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Text;

namespace TTFileArrangement
{
	[TypeConverter(typeof(CGIConverter))]
	[Serializable()]
	public class CGI : ICloneable
	{
		private bool path_TRANSLATED_flag = false;
		private TTFA.CommonClass.CharSplitStrings extensions;
		private bool enable = true;
		private string extensionsString = "";
		private bool firstLineRead = true;
		private string exePath = "";

		public CGI()
		{
		}

		[BrowsableAttribute(false)]
		public TTFA.CommonClass.CharSplitStrings Extensions
		{
			set
			{
				this.extensions = value;
			}
			get
			{
				return this.extensions;
			}
		}

		[Category("PATH_TRANSLATEDを有効にする")]
		public bool PATH_TRANSLATED
		{
			get { return path_TRANSLATED_flag; }
			set { path_TRANSLATED_flag = value; }
		}

		[Category("有効無効")]
		public bool Enable
		{
			get { return enable; }
			set { enable = value; }
		}

		[Category("実行する拡張子")]
		public string ExtensionsString
		{
			get { return extensionsString; }
			set {
				extensionsString = value;
				extensions = new TTFA.CommonClass.CharSplitStrings();
				if (extensions.SetCharSplitStringsString(value, ',') == false)
				{
					throw new Exception("書式が間違っています");
				}
			}
		}

		[Category("#!の行のパスを実行する")]
		public bool FirstLineRead
		{
			get { return firstLineRead; }
			set { 
				firstLineRead = value;
				if (firstLineRead == true)
					this.ExePath = "";
			}
		}

		[Category("実行パス"),
		Editor(typeof(System.Windows.Forms.Design.FileNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
		public string ExePath
		{
			get { return exePath; }
			set { exePath = value; }
		}

		public object Clone()
		{
			CGI CGI = new CGI();

			CGI.PATH_TRANSLATED = this.PATH_TRANSLATED;
			CGI.ExtensionsString = this.ExtensionsString;
			CGI.Enable = this.Enable;
			CGI.FirstLineRead = this.FirstLineRead;
			CGI.ExePath = this.ExePath;
			

			return CGI;	
		}
		/*
				// Meaningful text representation
				public override string ToString()
				{
					StringBuilder sb = new StringBuilder();
					sb.Append(this.LastName);
					sb.Append(",");
					sb.Append(this.FirstName);
					sb.Append(",");
					sb.Append(this.Age);
					sb.Append(",");
					sb.Append(this.Department);
					sb.Append(",");
					sb.Append(this.Role);
					return sb.ToString();
				}
				*/
	}

	[Serializable()]
	public class CGICollection : CollectionBase, ICustomTypeDescriptor, ICloneable
	{
		#region collection impl
		
		/// <summary>
		/// Adds an employee object to the collection
		/// </summary>
		/// <param name="emp"></param>
		public void Add( CGI emp )
		{
			this.List.Add( emp );
		}
		
		/// <summary>
		/// Removes an employee object from the collection
		/// </summary>
		/// <param name="emp"></param>
		public void Remove( CGI emp )
		{
			this.List.Remove( emp );
		}
		
		/// <summary>
		/// Returns an employee object at index position.
		/// </summary>
		public CGI this[ int index ] 
		{
			get
			{
				return (CGI)this.List[index];
			}
		}

		#endregion

		// Implementation of interface ICustomTypeDescriptor 
		#region ICustomTypeDescriptor impl 

		public String GetClassName()
		{
			return TypeDescriptor.GetClassName(this,true);
		}

		public AttributeCollection GetAttributes()
		{
			return TypeDescriptor.GetAttributes(this,true);
		}

		public String GetComponentName()
		{
			return TypeDescriptor.GetComponentName(this, true);
		}

		public TypeConverter GetConverter()
		{
			return TypeDescriptor.GetConverter(this, true);
		}

		public EventDescriptor GetDefaultEvent() 
		{
			return TypeDescriptor.GetDefaultEvent(this, true);
		}

		public PropertyDescriptor GetDefaultProperty() 
		{
			return TypeDescriptor.GetDefaultProperty(this, true);
		}

		public object GetEditor(Type editorBaseType) 
		{
			return TypeDescriptor.GetEditor(this, editorBaseType, true);
		}

		public EventDescriptorCollection GetEvents(Attribute[] attributes) 
		{
			return TypeDescriptor.GetEvents(this, attributes, true);
		}

		public EventDescriptorCollection GetEvents()
		{
			return TypeDescriptor.GetEvents(this, true);
		}

		public object GetPropertyOwner(PropertyDescriptor pd) 
		{
			return this;
		}


		/// <summary>
		/// Called to get the properties of this type. Returns properties with certain
		/// attributes. this restriction is not implemented here.
		/// </summary>
		/// <param name="attributes"></param>
		/// <returns></returns>
		public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			return GetProperties();
		}

		/// <summary>
		/// Called to get the properties of this type.
		/// </summary>
		/// <returns></returns>
		public PropertyDescriptorCollection GetProperties()
		{
			// Create a collection object to hold property descriptors
			PropertyDescriptorCollection pds = new PropertyDescriptorCollection(null);

			// Iterate the list of employees
			for( int i=0; i<this.List.Count; i++ )
			{
				// Create a property descriptor for the employee item and add to the property descriptor collection
				CGICollectionPropertyDescriptor pd = new CGICollectionPropertyDescriptor(this,i);
				pds.Add(pd);
			}
			// return the property descriptor collection
			return pds;
		}

		public object Clone()
		{
			CGICollection CGICollection = new CGICollection();

			foreach(CGI CGI in this.List)
			{
				CGICollection.Add(CGI);
			}

			return CGICollection;
		}

		public class CGICollectionPropertyDescriptor : PropertyDescriptor
		{
			private CGICollection collection = null;
			private int index = -1;

			public CGICollectionPropertyDescriptor(CGICollection coll, int idx) : 
				base( "#"+idx.ToString(), null )
			{
				this.collection = coll;
				this.index = idx;
			} 

			public override AttributeCollection Attributes
			{
				get 
				{ 
					return new AttributeCollection(null);
				}
			}

			public override bool CanResetValue(object component)
			{
				return true;
			}

			public override Type ComponentType
			{
				get 
				{ 
					return this.collection.GetType();
				}
			}

			public override string DisplayName
			{
				get 
				{
					CGI emp = this.collection[index]; 
					return "ABC";
				}
			}

			public override string Description
			{
				get
				{
					CGI emp = this.collection[index]; 
					StringBuilder sb = new StringBuilder();
				
					sb.Append(",");
				
					sb.Append(",");
				
					sb.Append(emp.Enable);
			
					return sb.ToString();
				}
			}

			public override object GetValue(object component)
			{
				return this.collection[index];
			}

			public override bool IsReadOnly
			{
				get { return false;  }
			}

			public override string Name
			{
				get { return "#"+index.ToString(); }
			}

			public override Type PropertyType
			{
				get { return this.collection[index].GetType(); }
			}

			public override void ResetValue(object component)
			{
			}

			public override bool ShouldSerializeValue(object component)
			{
				return true;
			}

			public override void SetValue(object component, object value)
			{
				// this.collection[index] = value;
			}

			
		}
		#endregion
	}

	internal class CGICollectionConverter : ExpandableObjectConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType )
		{
			if( destType == typeof(string) && value is CGICollection )
			{
				// Return department and department role separated by comma.
				return "ID&PASS";
			}
			return base.ConvertTo(context,culture,value,destType);
		}
	}

	internal class CGIConverter : ExpandableObjectConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType )
		{
			if( destType == typeof(string) && value is CGI )
			{
				// Cast the value to an Employee type
				CGI CGI = (CGI)value;

				// Return department and department role separated by comma.
				return CGI.ExtensionsString + " " + CGI.Enable.ToString();
			}
			return base.ConvertTo(context,culture,value,destType);
		}
	}

}

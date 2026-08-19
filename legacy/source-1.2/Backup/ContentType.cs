using System;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Text;

namespace TTFileArrangement
{
	[TypeConverter(typeof(ContentTypeConverter))]
	[Serializable()]
	public class ContentType : ICloneable
	{
		private string contentName = "";
		private string extension = "";

		public ContentType()
		{
		}

		[Category("ContentName"),
		Description("îºäpâpêîï∂éöÇæÇØÇ≈:ÇégópÇµÇ»Ç¢Ç≈Ç≠ÇæÇ≥Ç¢")]
		public string ContentName
		{
			get { return contentName; }
			set { contentName = value; }
		}

		[Category("PASS"),
		Description("îºäpâpêîï∂éöÇæÇØÇ≈:ÇégópÇµÇ»Ç¢Ç≈Ç≠ÇæÇ≥Ç¢")]
		public string Extension
		{
			get { return extension; }
			set { extension = value; }
		}

		public object Clone()
		{
			ContentType ContentType = new ContentType();

			ContentType.ContentName = this.ContentName;
			ContentType.extension = this.extension;

			return ContentType;	
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
	public class ContentTypeCollection : CollectionBase, ICustomTypeDescriptor, ICloneable
	{
		#region collection impl
		
		/// <summary>
		/// Adds an employee object to the collection
		/// </summary>
		/// <param name="emp"></param>
		public void Add( ContentType emp )
		{
			this.List.Add( emp );
		}
		
		/// <summary>
		/// Removes an employee object from the collection
		/// </summary>
		/// <param name="emp"></param>
		public void Remove( ContentType emp )
		{
			this.List.Remove( emp );
		}
		
		/// <summary>
		/// Returns an employee object at index position.
		/// </summary>
		public ContentType this[ int index ] 
		{
			get
			{
				return (ContentType)this.List[index];
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
				ContentTypeCollectionPropertyDescriptor pd = new ContentTypeCollectionPropertyDescriptor(this,i);
				pds.Add(pd);
			}
			// return the property descriptor collection
			return pds;
		}

		public object Clone()
		{
			ContentTypeCollection ContentTypeCollection = new ContentTypeCollection();

			foreach(ContentType ContentType in this.List)
			{
				ContentTypeCollection.Add(ContentType);
			}

			return ContentTypeCollection;
		}

		public class ContentTypeCollectionPropertyDescriptor : PropertyDescriptor
		{
			private ContentTypeCollection collection = null;
			private int index = -1;

			public ContentTypeCollectionPropertyDescriptor(ContentTypeCollection coll, int idx) : 
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
					ContentType emp = this.collection[index]; 
					return "ABC";
				}
			}

			public override string Description
			{
				get
				{
					ContentType emp = this.collection[index]; 
					StringBuilder sb = new StringBuilder();
				
					sb.Append(",");
				
					sb.Append(",");
				
					sb.Append(" years old, working for ");
					sb.Append(emp.ContentName);
					sb.Append(" as ");
					sb.Append(emp.Extension);
			
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

	internal class ContentTypeCollectionConverter : ExpandableObjectConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType )
		{
			if( destType == typeof(string) && value is ContentTypeCollection )
			{
				// Return department and department role separated by comma.
				return "ContentName&PASS";
			}
			return base.ConvertTo(context,culture,value,destType);
		}
	}

	internal class ContentTypeConverter : ExpandableObjectConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType )
		{
			if( destType == typeof(string) && value is ContentType )
			{
				// Cast the value to an Employee type
				ContentType ContentType = (ContentType)value;

				// Return department and department role separated by comma.
				return ContentType.ContentName + ":" + ContentType.Extension;
			}
			return base.ConvertTo(context,culture,value,destType);
		}
	}

}

using System;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Text;

namespace TTFileArrangement
{
	[TypeConverter(typeof(PersonConverter))]
	[Serializable()]
	public class Person : ICloneable
	{
		private string id = "";
		private string pass = "";

		public Person()
		{
		}

		[Category("ID"),
		Description("îºäpâpêîï∂éöÇæÇØÇ≈:ÇégópÇµÇ»Ç¢Ç≈Ç≠ÇæÇ≥Ç¢")]
		public string ID
		{
			get { return id; }
			set { id = value; }
		}

		[Category("PASS"),
		Description("îºäpâpêîï∂éöÇæÇØÇ≈:ÇégópÇµÇ»Ç¢Ç≈Ç≠ÇæÇ≥Ç¢")]
		public string Pass
		{
			get { return pass; }
			set { pass = value; }
		}

		public object Clone()
		{
			Person person = new Person();

			person.id = this.id;
			person.pass = this.pass;

			return person;	
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
	public class PersonCollection : CollectionBase, ICustomTypeDescriptor, ICloneable
	{
		#region collection impl
		
		/// <summary>
		/// Adds an employee object to the collection
		/// </summary>
		/// <param name="emp"></param>
		public void Add( Person emp )
		{
			this.List.Add( emp );
		}
		
		/// <summary>
		/// Removes an employee object from the collection
		/// </summary>
		/// <param name="emp"></param>
		public void Remove( Person emp )
		{
			this.List.Remove( emp );
		}
		
		/// <summary>
		/// Returns an employee object at index position.
		/// </summary>
		public Person this[ int index ] 
		{
			get
			{
				return (Person)this.List[index];
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
				PersonCollectionPropertyDescriptor pd = new PersonCollectionPropertyDescriptor(this,i);
				pds.Add(pd);
			}
			// return the property descriptor collection
			return pds;
		}

		public object Clone()
		{
			PersonCollection personCollection = new PersonCollection();

			foreach(Person person in this.List)
			{
				personCollection.Add(person);
			}

			return personCollection;
		}

		public class PersonCollectionPropertyDescriptor : PropertyDescriptor
		{
			private PersonCollection collection = null;
			private int index = -1;

			public PersonCollectionPropertyDescriptor(PersonCollection coll, int idx) : 
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
					Person emp = this.collection[index]; 
					return "ABC";
				}
			}

			public override string Description
			{
				get
				{
					Person emp = this.collection[index]; 
					StringBuilder sb = new StringBuilder();
				
					sb.Append(",");
				
					sb.Append(",");
				
					sb.Append(" years old, working for ");
					sb.Append(emp.ID);
					sb.Append(" as ");
					sb.Append(emp.Pass);
			
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

	internal class PersonCollectionConverter : ExpandableObjectConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType )
		{
			if( destType == typeof(string) && value is PersonCollection )
			{
				// Return department and department role separated by comma.
				return "ID&PASS";
			}
			return base.ConvertTo(context,culture,value,destType);
		}
	}

	internal class PersonConverter : ExpandableObjectConverter
	{
		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destType )
		{
			if( destType == typeof(string) && value is Person )
			{
				// Cast the value to an Employee type
				Person person = (Person)value;

				// Return department and department role separated by comma.
				return person.ID + ":" + person.Pass;
			}
			return base.ConvertTo(context,culture,value,destType);
		}
	}

}

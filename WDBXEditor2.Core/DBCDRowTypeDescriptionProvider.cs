using DBCD;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WDBXEditor2.Core
{

    public class DBCDRowProxy
    {
        public DBCDRowProxy() { }  
        public DBCDRowProxy(DBCDRow rowData)
        {
            RowData = rowData;
        }
        public DBCDRow? RowData { get; set; }
    }

    public class DBCDRowTypeDescriptionProvider : TypeDescriptionProvider
    {
        private readonly Type _rowUnderlyingType;
        public DBCDRowTypeDescriptionProvider(Type rowUnderlyingType)
        {
            _rowUnderlyingType = rowUnderlyingType;
        }

        public override ICustomTypeDescriptor GetTypeDescriptor([DynamicallyAccessedMembers((DynamicallyAccessedMemberTypes)(-1))] Type objectType, object? instance)
        {
            return new DBCDRowTypeDescriptor(DBCDHelper.GetColumnNames(_rowUnderlyingType));
        }
    }

    public class DBCDRowTypeDescriptor : CustomTypeDescriptor
    {
        private readonly ICollection<DBCDRowPropertyDescriptor> _propertyDescriptors;


        public DBCDRowTypeDescriptor(string[] columnNames)
        {
            _propertyDescriptors = columnNames.Select(x => new DBCDRowPropertyDescriptor(x)).ToList();
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return new PropertyDescriptorCollection(_propertyDescriptors.ToArray());
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        {
            return GetProperties();
        }
    }

    public class DBCDRowPropertyDescriptor : PropertyDescriptor
    {
        public DBCDRowPropertyDescriptor(string name) : base(name, null)
        {

        }

        public override bool CanResetValue(object component)
        {
            throw new NotImplementedException();
        }

        public override Type ComponentType
        {
            get { throw new NotImplementedException(); }
        }

        public override object? GetValue(object? component)
        {
            DBCDRow? rowData;
            if (component is DBCDRowProxy proxy)
            {
                rowData = proxy.RowData;
            } else
            {
                rowData = component as DBCDRow;
            }
            if (rowData == null)
            {
                return null;
            }
            return DBCDHelper.GetDBCRowColumn(rowData, Name);
        }

        public override bool IsReadOnly
        {
            get { return false; }
        }

        public override Type PropertyType
        {
            get { return typeof(object); }
        }

        public override void ResetValue(object component)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(object? component, object? value)
        {
            var oldValue = GetValue(component);

            if (oldValue != value)
            {
                DBCDRow? rowData;
                if (component is DBCDRowProxy proxy)
                {
                    rowData = proxy.RowData;
                }
                else
                {
                    rowData = component as DBCDRow;
                }
                if (rowData != null && value != null)
                {
                    DBCDHelper.SetDBCRowColumn(rowData, Name, value);
                    OnValueChanged(component, new PropertyChangedEventArgs(base.Name));
                }
            }
        }

        public override bool ShouldSerializeValue(object component)
        {
            throw new NotImplementedException();
        }
    }
}

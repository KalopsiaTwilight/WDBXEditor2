using DBCD;
using DBCD.IO.Attributes;
using System.Dynamic;
using System.Reflection;

namespace WDBXEditor2.Core
{
    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsId { get; set; }  
        public bool IsArray { get; set; }
        public bool IsRelation { get; set; }    
        public int ArraySize { get; set; }
        public int BitSize { get; set; }

        public string DataType { get; set; } = string.Empty;
        public bool ReferencesForeignTable { get; set; }
        public string ForeignTableName = string.Empty;
        public string ForeignColumnName = string.Empty;

        public override string ToString()
        {
            var attributeString = "";
            if (IsId)
            {
                attributeString += "ID, ";
            }
            if (IsRelation)
            {
                attributeString += "Relation, ";
            }
            if (IsArray)
            {
                attributeString += "Array, ";
            }

            if (attributeString.Length > 0)
            {
                attributeString = attributeString.Substring(0, attributeString.Length - 2);
            }

            var result = $"{DataType}";
            if (BitSize > 0)
            {
                result += $" - {BitSize} bits";
            }
            if (attributeString.Length > 0)
            {
                result += $" - {attributeString}";
            }
            if (ReferencesForeignTable)
            {
                result += $" - References {ForeignTableName}[{ForeignColumnName}]";
            }

            return result;
        }
    }

    public class DBCDHelper
    {
        public static Type GetUnderlyingType(IDBCDStorage storage)
        {
            return storage.GetType().GetGenericArguments()[0];
        }

        public static string[] GetColumnNames(IDBCDStorage storage)
        {
            var row = storage.Values.FirstOrDefault();
            if (row == null)
            {
                return GetColumnNames(GetUnderlyingType(storage));
            }
            else
            {
                var fields = row.GetDynamicMemberNames();
                return fields.SelectMany(fieldName =>
                {
                    if (row[fieldName].GetType().IsArray)
                    {
                        var cardinality = ((Array)row[fieldName]).Length;
                        if (cardinality > 1)
                        {
                            var result = new string[cardinality];
                            for (int i = 0; i < result.Length; i++)
                            {
                                result[i] = fieldName + i;
                            }
                            return result;
                        }
                    } 
                    return [fieldName];
                }).ToArray();
            }
        }

        public static string[] GetColumnNames(Type underlyingType)
        {
            var fields = underlyingType.GetFields();

            return fields
                .SelectMany(field =>
                {
                    if (field.FieldType.IsArray)
                    {
                        var count = field.GetCustomAttribute<CardinalityAttribute>()!.Count;
                        var result = new string[count];
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = field.Name + i;
                        }
                        return result;
                    }
                    return [field.Name];
                })
                .ToArray();
        }

        public static object ConvertArray(Type type, int size, string[] records)
        {
            return _arrayConverters[type](size, records);
        }

        public static void SetDBCRowColumn(DBCDRow row, string colName, object value)
        {
            var fieldName = GetUnderlyingFieldName(row.GetUnderlyingType(), colName, out var arrayIndex);
            var field = row.GetUnderlyingType().GetField(fieldName) ?? throw new InvalidOperationException("Invalid column name specified: " + colName);
            if (field.FieldType.IsArray)
            {
                ((Array)row[fieldName]).SetValue(Convert.ChangeType(value, field.FieldType.GetElementType()!), arrayIndex);
            }
            else
            {
                row[fieldName] = Convert.ChangeType(value, field.FieldType);
            }
            if (field.GetCustomAttribute<IndexAttribute>() != null)
            {
                row.ID = Convert.ToInt32(value);
            }
        }

        public static object GetDBCRowColumn(DBCDRow row, string colName)
        {
            var fieldName = GetUnderlyingFieldName(row.GetUnderlyingType(), colName, out var arrayIndex);
            var value = row[fieldName];
            if (value.GetType().IsArray)
            {
                return ((Array)row[fieldName]).GetValue(arrayIndex)!;
            }
            else
            {
                return row[fieldName];
            }
        }

        public static Type GetTypeForColumn(DBCDRow row, string fieldName)
        {
            return GetTypeForColumn(row.GetUnderlyingType(), fieldName);
        }

        public static Type GetTypeForColumn(Type dbcdType, string colName)
        {
            var field = dbcdType.GetField(GetUnderlyingFieldName(dbcdType, colName, out _)) ?? throw new InvalidOperationException("Invalid column name specified: " + colName);
            if (field.FieldType.IsArray)
            {
                return field.FieldType.GetElementType()!;
            }
            return field.FieldType;
        }

        public static string GetUnderlyingFieldName(Type type, string fieldName, out int index)
        {
            index = 0;
            if (type.GetField(fieldName) != null)
            {
                return fieldName;
            }

            var n = 1;
            while (int.TryParse(fieldName[^1].ToString(), out var indexN))
            {
                fieldName = fieldName.Substring(0, fieldName.Length - 1);
                index += n * indexN;
                n *= 10;
            }
            return fieldName;
        }
        public static string GetIdFieldName(IDBCDStorage storage)
        {
            var type = GetUnderlyingType(storage);
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<IndexAttribute>() != null)
                {
                    return field.Name;
                }
            }
            return string.Empty;
        }

        public static DBCDRow ConstructNewRow(IDBCDStorage storage)
        {
            var id = storage.Keys.Count > 0 ? storage.Keys.Max() + 1 : 1;
            var rowData = storage.ConstructRow(id);
            rowData[GetIdFieldName(storage)] = id;
            rowData.ID = id;

            // Resize arrays to their actual read size
            var firstRow = storage.Values.FirstOrDefault();
            if (firstRow != null)
            {
                var arrayFields = rowData.GetUnderlyingType().GetFields().Where(x => x.FieldType.IsArray);
                foreach (var arrayField in arrayFields)
                {
                    var count = ((Array)firstRow[arrayField.Name]).Length;
                    Array arrayData = Array.CreateInstance(arrayField.FieldType.GetElementType()!, count);
                    for (var i = 0; i < count; i++)
                    {
                        arrayData.SetValue(Activator.CreateInstance(arrayField.FieldType.GetElementType()!), i);
                    }
                    rowData[arrayField.Name] = arrayData;
                }
            }

            return rowData;
        }

        public static ColumnInfo GetColumnInfo(Type underlyingType, string columnName)
        {
            var fieldName = GetUnderlyingFieldName(underlyingType, columnName, out int _);
            var fieldInfo = underlyingType.GetField(fieldName)!;

            var columnInfo = new ColumnInfo
            {
                Name = columnName,
                IsId = fieldInfo.GetCustomAttribute<IndexAttribute>() != null,
                IsRelation = fieldInfo.GetCustomAttribute<RelationAttribute>() != null
            };
            
            var cardinalityAttribute = fieldInfo.GetCustomAttribute<CardinalityAttribute>();
            columnInfo.IsArray = cardinalityAttribute != null;
            columnInfo.ArraySize = cardinalityAttribute?.Count ?? -1;
            columnInfo.DataType = GetSimplifiedDataTypeName(fieldInfo.FieldType);

            var referenceAttribute = fieldInfo.GetCustomAttribute<ForeignReferenceAttribute>();
            if (referenceAttribute != null)
            {
                columnInfo.ReferencesForeignTable = true;
                columnInfo.ForeignTableName = referenceAttribute.ForeignTable;
                columnInfo.ForeignColumnName = referenceAttribute.ForeignColumn;
            }

            var bitSizeAttribute = fieldInfo.GetCustomAttribute<SizeInBitsAttribute>();
            if (bitSizeAttribute != null)
            {
                columnInfo.BitSize = bitSizeAttribute.Size;
            } else
            {
                columnInfo.BitSize = GetBitSize(fieldInfo.FieldType);
            }
            return columnInfo;
        }

        private static object ConvertArray<TConvert>(int size, string[] records)
        {
            var result = new TConvert[size];
            for (var i = 0; i < size; i++)
            {
                result[i] = (TConvert)Convert.ChangeType(records[i], typeof(TConvert));
            }
            return result;
        }

        private static Dictionary<Type, Func<int, string[], object>> _arrayConverters = new Dictionary<Type, Func<int, string[], object>>()
        {
            [typeof(ulong[])] = ConvertArray<ulong>,
            [typeof(long[])] = ConvertArray<long>,
            [typeof(float[])] = ConvertArray<float>,
            [typeof(int[])] = ConvertArray<int>,
            [typeof(uint[])] = ConvertArray<uint>,
            [typeof(short[])] = ConvertArray<short>,
            [typeof(ushort[])] = ConvertArray<ushort>,
            [typeof(byte[])] = ConvertArray<byte>,
            [typeof(sbyte[])] = ConvertArray<sbyte>,
            [typeof(string[])] = ConvertArray<string>,
        };

        private static string GetSimplifiedDataTypeName(Type t)
        {
            if (t.IsArray)
            {
                t = t.GetElementType()!;
            }
            switch (t.Name)
            {
                case nameof(UInt64): return "unsigned integer";
                case nameof(Int64): return "integer";
                case nameof(Single): return "float";
                case nameof(Int32): return "integer";
                case nameof(UInt32): return "unsigned integer";
                case nameof(Int16): return "integer";
                case nameof(UInt16): return "unsigned integer";
                case nameof(Byte): return "unsigned integer";
                case nameof(SByte): return "integer";
                case nameof(String): return "string";
            }
            return string.Empty;
        }

        private static int GetBitSize(Type t)
        {
            switch (t.Name)
            {
                case nameof(UInt64): return 64;
                case nameof(Int64): return 64;
                case nameof(Single): return 32;
                case nameof(Int32): return 32;
                case nameof(UInt32): return 32;
                case nameof(Int16): return 16;
                case nameof(UInt16): return 16;
                case nameof(Byte): return 8;
                case nameof(SByte): return 8;
            }
            return 0;
        }
    }
}

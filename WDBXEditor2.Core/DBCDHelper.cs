using DBCD;
using DBCD.IO.Attributes;
using System.Reflection;

namespace WDBXEditor2.Core
{
    public class DBCDHelper
    {
        public static Type GetUnderlyingType(IDBCDStorage storage)
        {
            return storage.GetType().GetGenericArguments()[0];
        }
        public static string[] GetColumnNames(IDBCDStorage storage)
        {
            return GetColumnNames(GetUnderlyingType(storage));
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
    }
}

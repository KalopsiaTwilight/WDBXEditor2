using DBCD;
using DBCD.IO.Attributes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Windows.Documents;

namespace WDBXEditor2.Helpers
{
    internal class DBCDRowHelper
    {
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

        public static object ConvertArray(Type type, int size, string[] records)
        {
            return _arrayConverters[type](size, records);
        }

        public static void SetDBCRowColumn(DBCDRow row, string colName, object value)
        {
            var fieldName = GetUnderlyingFieldName(row.GetUnderlyingType(), colName, out var arrayIndex);
            var field = row.GetUnderlyingType().GetField(fieldName);
            if (field.FieldType.IsArray)
            {
                ((Array)row[fieldName]).SetValue(Convert.ChangeType(value, field.FieldType.GetElementType()), arrayIndex);
            } else
            {
                row[fieldName] = Convert.ChangeType(value, field.FieldType);
            }
        }

        public static object GetDBCRowColumn(DBCDRow row, string colName)
        {
            var fieldName = GetUnderlyingFieldName(row.GetUnderlyingType(), colName, out var arrayIndex);
            var field = row.GetUnderlyingType().GetField(fieldName);
            if (field.FieldType.IsArray)
            {
                return ((Array)row[fieldName]).GetValue(arrayIndex);
            } else
            {
                return row[fieldName];
            }
        }

        public static Type GetFieldType(DBCDRow row, string fieldName)
        {
            return GetFieldType(row.GetUnderlyingType(), fieldName);
        }

        public static Type GetFieldType(Type t, string fieldName)
        {
            var field = t.GetField(GetUnderlyingFieldName(t, fieldName, out _));
            if (field.FieldType.IsArray)
            {
                return field.FieldType.GetElementType();
            }
            return field.FieldType;
        }

        public static string[] GetColumnNames(IDBCDStorage storage)
        {
            var underlyingType = storage.GetType().GetGenericArguments()[0];
            var fieldNames = storage.AvailableColumns;

            return fieldNames
                .SelectMany(name =>
                {
                    var field = underlyingType.GetField(name);
                    if (field.FieldType.IsArray)
                    {
                        var count = field.GetCustomAttribute<CardinalityAttribute>().Count;
                        var result = new string[count];
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = name + i;
                        }
                        return result;
                    }
                    return new[] { name };
                })
                .ToArray();
        }


    private static string GetUnderlyingFieldName(Type type, string fieldName, out int index)
        {
            index = 0;
            var n = 1;
            while (int.TryParse(fieldName[^1].ToString(), out var indexN))
            {
                fieldName = fieldName.Substring(0, fieldName.Length - 1);
                index += n * indexN;
                n *= 10;
            }
            return fieldName;
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
    }
}

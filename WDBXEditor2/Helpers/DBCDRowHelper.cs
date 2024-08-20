using DBCD;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Reflection;

namespace WDBXEditor2.Helpers
{
    internal class DBCDRowHelper
    {
        private static Dictionary<Type, Func<int, string[], object>> _arrayConverters = new Dictionary<Type, Func<int, string[], object>>()
        {
            [typeof(ulong[])] = (size, records) => ConvertArray<ulong>(size, records),
            [typeof(long[])] = (size, records) => ConvertArray<long>(size, records),
            [typeof(float[])] = (size, records) => ConvertArray<float>(size, records),
            [typeof(int[])] = (size, records) => ConvertArray<int>(size, records),
            [typeof(uint[])] = (size, records) => ConvertArray<uint>(size, records),
            [typeof(ulong[])] = (size, records) => ConvertArray<ulong>(size, records),
            [typeof(short[])] = (size, records) => ConvertArray<short>(size, records),
            [typeof(ushort[])] = (size, records) => ConvertArray<ushort>(size, records),
            [typeof(byte[])] = (size, records) => ConvertArray<byte>(size, records),
            [typeof(sbyte[])] = (size, records) => ConvertArray<sbyte>(size, records),
            [typeof(string[])] = (size, records) => ConvertArray<string>(size, records),
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
            var field = row.GetUnderlyingType().GetField(GetUnderlyingFieldName(row.GetUnderlyingType(), fieldName, out _));
            if (field.FieldType.IsArray)
            {
                return field.FieldType.GetElementType();
            }
            return field.FieldType;
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

namespace DBXPatching.Core
{
    internal class ConvertHelper
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

        public static object ConvertValue (Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName);
            if (field == null)
            {
                var index = 0;
                var n = 1;
                var strippedFieldName = fieldName;
                while (int.TryParse(strippedFieldName[^1].ToString(), out var indexN))
                {
                    strippedFieldName = strippedFieldName.Substring(0, strippedFieldName.Length - 1);
                    index += n * indexN;
                    n *= 10;
                }
                field = type.GetField(fieldName);
                if (field == null)
                {
                    throw new ArgumentException("Could not find a field with fieldname: " + fieldName);
                }
                return Convert.ChangeType(value, field.FieldType.GetElementType()!);
            }
            else
            {
                return Convert.ChangeType(value, field.FieldType);
            }
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

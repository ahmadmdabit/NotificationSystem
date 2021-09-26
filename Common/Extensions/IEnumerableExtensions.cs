using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Common.Extensions
{
    public static class IEnumerableExtensions
    {
        public static DataTable ToDataTable<T>(this T var, Dictionary<string, Type> columns = null)
        {
            return ToDataTable<T>(new HashSet<T> { var }, columns);
        }

        public static DataTable ToDataTable<T>(this IEnumerable<T> varlist, Dictionary<string, Type> columns = null)
        {
            DataTable dtReturn = new DataTable();

            // column names
            PropertyInfo[] oProps = null;

            if (varlist == null) return dtReturn;

            foreach (T rec in varlist)
            {
                if (oProps == null)
                {
                    oProps = ((Type)rec.GetType()).GetProperties();
                }

                if (dtReturn.Columns.Count == 0)
                {
                    if (columns == null)
                    {
                        // Use reflection to get property names, to create table, Only first time, others will follow
                        foreach (PropertyInfo pi in oProps)
                        {
                            Type colType = pi.PropertyType;

                            if ((colType.IsGenericType) && (colType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                            {
                                colType = colType.GetGenericArguments()[0];
                            }

                            dtReturn.Columns.Add(new DataColumn(pi.Name, colType));
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, Type> column in columns)
                        {
                            dtReturn.Columns.Add(new DataColumn(column.Key, column.Value));
                        }
                    }
                }

                DataRow dr = dtReturn.NewRow();

                foreach (PropertyInfo pi in oProps)
                {
                    if (dtReturn.Columns.Contains(pi.Name))
                    {
                        dr[pi.Name] = pi.GetValue(rec, null) == null ? DBNull.Value : pi.GetValue(rec, null);
                    }
                }

                dtReturn.Rows.Add(dr);
            }
            return dtReturn;
        }
    }
}
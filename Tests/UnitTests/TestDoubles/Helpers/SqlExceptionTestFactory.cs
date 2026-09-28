using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Data.SqlClient;

namespace TestDoubles.Helpers;

public static class SqlExceptionTestFactory
{
    public static SqlException Create(int errorNumber, string message = "Simulated SQL exception")
    {
        var sqlErrorType = typeof(SqlError);
        var sqlErrorCtor = sqlErrorType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        object? sqlError = null;
        if (sqlErrorCtor is not null)
        {
            var parameters = sqlErrorCtor.GetParameters();
            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                if (i == 0 && pType == typeof(int)) args[i] = errorNumber;
                else if (pType == typeof(int)) args[i] = 0;
                else if (pType == typeof(byte)) args[i] = (byte)0;
                else if (pType == typeof(string)) args[i] = message;
                else if (pType == typeof(uint)) args[i] = (uint)0;
                else args[i] = null;
            }
            sqlError = sqlErrorCtor.Invoke(args);
        }

        var collectionType = typeof(SqlErrorCollection);
        var collectionCtor = collectionType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Length == 0);
        var collection = collectionCtor?.Invoke(null)
            ?? RuntimeHelpers.GetUninitializedObject(collectionType);

        if (sqlError is not null)
        {
            var addMethod = collectionType.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
            addMethod?.Invoke(collection, [sqlError]);
        }

        var sqlExType = typeof(SqlException);
        var createMethod = sqlExType.GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static, [collectionType, typeof(string)]);
        if (createMethod is not null)
        {
            return (SqlException)createMethod.Invoke(null, [collection, "11.0.0"])!;
        }

        var ctor = sqlExType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Length >= 2 && c.GetParameters()[1].ParameterType == collectionType);
        if (ctor is not null)
        {
            var args = new object?[ctor.GetParameters().Length];
            args[0] = message;
            args[1] = collection;
            return (SqlException)ctor.Invoke(args);
        }

        var exception = (SqlException)RuntimeHelpers.GetUninitializedObject(sqlExType);
        sqlExType.GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(exception, collection);
        return exception;
    }
}

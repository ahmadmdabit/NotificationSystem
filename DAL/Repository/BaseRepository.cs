using Common.Helpers;
using DAL.Entity;
using Dapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repository
{
    public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity, new()
    {
        protected readonly IDbConnection _dbConnection;
        protected readonly string _tableName;

        protected BaseRepository(IDbConnection dbConnection)
        {
            this._dbConnection = dbConnection;
            this._tableName = new T().GetTableName();

            SqlMapper.AddTypeMap(typeof(DateTime), System.Data.DbType.DateTime2); // ref: https://stackoverflow.com/a/34030875
            SqlMapper.AddTypeMap(typeof(DateTime?), System.Data.DbType.DateTime2); // ref: https://stackoverflow.com/a/34030875
        }

        private IEnumerable<PropertyInfo> GetProperties => typeof(T).GetProperties();

        public async Task<IEnumerable<T>> GetAsync(string include = "*")
        {
            return await this._dbConnection.QueryAsync<T>($"SELECT {include} FROM {_tableName}").ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetAsync(string prop, object value, string include = "*")
        {
            return await this._dbConnection.QueryAsync<T>($"SELECT {include} FROM {_tableName} WHERE [{prop}]=@value", new { value = value }).ConfigureAwait(false);
        }

        public async Task<T> GetAsync(long id, string include = "*")
        {
            var result = await this._dbConnection.QuerySingleOrDefaultAsync<T>($"SELECT {include} FROM {_tableName} WHERE Id=@Id", new { Id = id }).ConfigureAwait(false);
            if (result == null)
                throw new KeyNotFoundException($"{_tableName} with id [{id}] could not be found.");

            return result;
        }

        public async Task<bool> DeleteAsync(long id)
        {
            return await this._dbConnection.ExecuteAsync($"DELETE FROM {_tableName} WHERE Id=@Id", new { Id = id }).ConfigureAwait(false) > 0;
        }

        public async Task<int> InsertAsync(IEnumerable<T> list, string[] exclude = null)
        {
            return await this._dbConnection.ExecuteAsync(GenerateInsertQuery(exclude), list).ConfigureAwait(false);
        }

        public async Task<T> InsertAsync(T t, string[] exclude = null)
        {
            return (await this._dbConnection.QueryAsync<T>(GenerateInsertQuery(exclude), t).ConfigureAwait(false)).SingleOrDefault();
        }

        protected string GenerateInsertQuery(string[] exclude = null)
        {
            if (exclude == null)
            {
                exclude = new string[] { "Id", "CreatedAt", "UpdatedAt", "IsDeleted" };
            }

            var insertQuery = new StringBuilder($"INSERT INTO {_tableName} ");

            insertQuery.Append("(");

            var properties = GenerateListOfProperties(GetProperties);
            
            properties.ForEach(prop =>
            {
                if (exclude?.Any(x => x == prop) == false)
                {
                    insertQuery.Append($"[{prop}],");
                }
            });

            insertQuery
                .Remove(insertQuery.Length - 1, 1)
                .Append(") VALUES (");

            properties.ForEach(prop =>
            {
                if (exclude?.Any(x => x == prop) == false)
                {
                    insertQuery.Append($"@{prop},");
                }
            });

            insertQuery
                .Remove(insertQuery.Length - 1, 1)
                .Append(");")
                
                .Append($"SELECT * FROM {_tableName} WHERE Id=SCOPE_IDENTITY();");

            return insertQuery.ToString();
        }

        protected static List<string> GenerateListOfProperties(IEnumerable<PropertyInfo> listOfProperties)
        {
            return listOfProperties.Where(x =>
            {
                var attributes = x.GetCustomAttributes(typeof(DescriptionAttribute), false);
                return attributes.Length <= 0 || (attributes[0] as DescriptionAttribute)?.Description != "ignore";
            }).Select(x => x.Name).ToList();
        }

        public async Task<T> UpdateAsync(T t, string[] exclude = null)
        {
            return (await this._dbConnection.QueryAsync<T>(GenerateUpdateQuery(exclude), t).ConfigureAwait(false)).SingleOrDefault();
        }

        protected string GenerateUpdateQuery(string[] exclude = null)
        {
            if (exclude == null)
            {
                exclude = new string[] { "Id", "CreatedAt", "UpdatedAt", "IsDeleted" };
            }

            var updateQuery = new StringBuilder($"UPDATE {_tableName} SET ");
            var properties = GenerateListOfProperties(GetProperties);

            properties.ForEach(property =>
            {
                if (exclude?.Any(x => x == property) == false)
                {
                    updateQuery.Append($"{property}=@{property},");
                }
            });

            updateQuery.Remove(updateQuery.Length - 1, 1); //remove last comma
            updateQuery.Append(" WHERE Id=@Id;")
                
                .Append("SELECT * FROM {_tableName} WHERE Id=@Id;");

            return updateQuery.ToString();
        }

        public async Task<SpResult> QueryAsync(string sql, DynamicParameters parameters, CommandType commandType = CommandType.StoredProcedure)
        {
            var results = await this._dbConnection.QueryAsync(sql, parameters, commandType: commandType).ConfigureAwait(false);
            return new SpResult
            {
                Success = parameters?.Get<bool>("SP_Success") == true,
                Message = parameters?.Get<string>("SP_Message"),
                Data = results?.ToList()
            };
        }

        public async Task<SpResult> QueryMultipleAsync(string sql, DynamicParameters parameters, CommandType commandType = CommandType.StoredProcedure)
        {
            var results = await this._dbConnection.QueryMultipleAsync(sql, parameters, commandType: commandType).ConfigureAwait(false);
            List<object> data = new List<object>();
            try
            {
                if (results != null)
                {
                    object result;
                    do
                    {
                        result = await results.ReadAsync().ConfigureAwait(false);
                        if (result != null)
                        {
                            data.Add(result);
                        }
                    } while (result != null);
                }
            }
            catch (System.Exception ex) when (ex.Message.StartsWith("The reader has been disposed;") || ex.Message.StartsWith("No columns were selected"))
            {
            }
            return new SpResult
            {
                Success = parameters?.Get<bool>("SP_Success") == true,
                Message = parameters?.Get<string>("SP_Message"),
                Data = data?.Count == 1 ? data[0] : data
            };
        }
    }
}
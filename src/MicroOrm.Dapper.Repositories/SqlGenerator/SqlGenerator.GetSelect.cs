using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace MicroOrm.Dapper.Repositories.SqlGenerator
{
    /// <inheritdoc />
    public partial class SqlGenerator<TEntity>
        where TEntity : class
    {
        private SqlQuery GetSelect(Expression<Func<TEntity, bool>> predicate, bool firstOnly,
            params Expression<Func<TEntity, object>>[] includes)
        {
            var sqlQuery = InitBuilderSelect(firstOnly);

            var joinsBuilder = AppendJoinToSelect(sqlQuery, includes);
            sqlQuery.SqlBuilder
                .Append(" FROM ")
                .Append(TableName)
                .Append(" ");

            if (includes.Any())
                sqlQuery.SqlBuilder.Append(joinsBuilder);

            AppendWherePredicateQuery(sqlQuery, predicate, QueryType.Select);

            SetOrder(sqlQuery);

            if (firstOnly && (Config.SqlProvider == SqlProvider.MySQL || Config.SqlProvider == SqlProvider.PostgreSQL))
                sqlQuery.SqlBuilder.Append(" LIMIT 1");
            else
                SetLimit(sqlQuery);
            
            return sqlQuery;
        }

        private void SetLimit(SqlQuery sqlQuery)
        {
            if (FilterData.LimitInfo == null)
                return;

            sqlQuery.SqlBuilder.Append(FilterData.LimitInfo.Offset != null
                ? $" LIMIT {FilterData.LimitInfo.Offset.Value},{FilterData.LimitInfo.Limit}"
                : $" LIMIT {FilterData.LimitInfo.Limit}");

            if (!FilterData.LimitInfo.Permanent)
                FilterData.LimitInfo = null;
        }

        /// <summary>
        /// Set order by in query; DapperRepository.SetOrderBy must be called first. 
        /// </summary>
        private void SetOrder(SqlQuery sqlQuery)
        {
            if (FilterData.OrderInfo == null) return;

            sqlQuery.SqlBuilder.Append("ORDER BY ");
            
            var count = FilterData.OrderInfo.Columns.Count;
            for (var i = 0; i < count; i++)
            {
                var col = FilterData.OrderInfo.Columns[i];
                if (i >= count - 1)
                {
                    sqlQuery.SqlBuilder.Append($"{col} {FilterData.OrderInfo.Direction} ");
                    break;
                }

                sqlQuery.SqlBuilder.Append($"{col},");
            }
            
            if (!FilterData.OrderInfo.Permanent)
                FilterData.OrderInfo = null;
        }

        /// <inheritdoc />
        public virtual SqlQuery GetSelectFirst(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] includes)
        {
            return GetSelect(predicate, true, includes);
        }

        /// <inheritdoc />
        public virtual SqlQuery GetSelectAll(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] includes)
        {
            return GetSelect(predicate, false, includes);
        }
        
        /// <inheritdoc />
        public SqlQuery GetSelectById(object id, params Expression<Func<TEntity, object>>[] includes)
        {
            if (KeySqlProperties.Length != 1)
                throw new NotSupportedException("GetSelectById support only 1 key");

            var keyProperty = KeySqlProperties[0];

            var sqlQuery = InitBuilderSelect(true);

            if (includes.Any())
            {
                var joinsBuilder = AppendJoinToSelect(sqlQuery, includes);
                sqlQuery.SqlBuilder
                    .Append(" FROM ")
                    .Append(TableName)
                    .Append(" ");

                sqlQuery.SqlBuilder.Append(joinsBuilder);
            }
            else
            {
                sqlQuery.SqlBuilder
                    .Append(" FROM ")
                    .Append(TableName)
                    .Append(" ");
            }

            IDictionary<string, object> dictionary = new Dictionary<string, object>
            {
                {keyProperty.PropertyName, id}
            };

            sqlQuery.SqlBuilder
                .Append("WHERE ")
                .Append(TableName)
                .Append(".")
                .Append(keyProperty.ColumnName)
                .Append(" = @")
                .Append(keyProperty.PropertyName)
                .Append(" ");

            if (LogicalDelete)
                sqlQuery.SqlBuilder
                    .Append("AND ")
                    .Append(TableName)
                    .Append(".")
                    .Append(StatusPropertyName)
                    .Append(" != ")
                    .Append(LogicalDeleteValue)
                    .Append(" ");

            if (Config.SqlProvider == SqlProvider.MySQL || Config.SqlProvider == SqlProvider.PostgreSQL)
                sqlQuery.SqlBuilder.Append("LIMIT 1");

            sqlQuery.SetParam(dictionary);
            return sqlQuery;
        }

        /// <inheritdoc />
        public virtual SqlQuery GetSelectBetween(object from, object to, Expression<Func<TEntity, object>> btwField)
        {
            return GetSelectBetween(from, to, btwField, null);
        }

        /// <inheritdoc />
        public virtual SqlQuery GetSelectBetween(object from, object to, Expression<Func<TEntity, object>> btwField, Expression<Func<TEntity, bool>> predicate)
        {
            var fieldName = ExpressionHelper.GetPropertyName(btwField);
            var columnName = SqlProperties.First(x => x.PropertyName == fieldName).ColumnName;
            var query = GetSelectAll(predicate);

            query.SqlBuilder
                .Append(predicate == null && !LogicalDelete ? "WHERE" : "AND")
                .Append(" ")
                .Append(TableName)
                .Append(".")
                .Append(columnName)
                .Append(" BETWEEN '")
                .Append(from)
                .Append("' AND '")
                .Append(to)
                .Append("'");

            return query;
        }

        private SqlQuery InitBuilderSelect(bool firstOnly)
        {
            var query = new SqlQuery();
            query.SqlBuilder.Append("SELECT ");
            if (firstOnly && Config.SqlProvider == SqlProvider.MSSQL)
                query.SqlBuilder.Append("TOP 1 ");

            query.SqlBuilder.Append(GetFieldsSelect(TableName, SqlProperties));

            return query;
        }

        private static string GetFieldsSelect(string tableName, SqlPropertyMetadata[] properties)
        {
            //Projection function
            string ProjectionFunction(SqlPropertyMetadata p)
            {
                return !string.IsNullOrEmpty(p.Alias)
                    ? string.Format("{0}.{1} AS {2}", tableName, p.ColumnName, p.PropertyName)
                    : string.Format("{0}.{1}", tableName, p.ColumnName);
            }

            return string.Join(", ", properties.Select(ProjectionFunction));
        }
    }
}

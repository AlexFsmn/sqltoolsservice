﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlTools.ServiceLayer.Connection;
using Microsoft.SqlTools.ServiceLayer.QueryExecution.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.QueryExecution
{
    public class Query //: IDisposable
    {
        #region Properties

        public string QueryText { get; set; }

        public ConnectionInfo EditorConnection { get; set; }

        private readonly CancellationTokenSource cancellationSource;

        public List<ResultSet> ResultSets { get; set; }

        public ResultSetSummary[] ResultSummary
        {
            get
            {
                return ResultSets.Select((set, index) => new ResultSetSummary
                {
                    ColumnInfo = set.Columns,
                    Id = index,
                    RowCount = set.Rows.Count
                }).ToArray();
            }
        }

        public bool HasExecuted { get; set; }

        #endregion

        public Query(string queryText, ConnectionInfo connection)
        {
            // Sanity check for input
            if (queryText == null)
            {
                throw new ArgumentNullException(nameof(queryText), "Query text cannot be null");
            }
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection), "Connection cannot be null");
            }

            // Initialize the internal state
            QueryText = queryText;
            EditorConnection = connection;
            HasExecuted = false;
            ResultSets = new List<ResultSet>();
            cancellationSource = new CancellationTokenSource();
        }

        public async Task Execute()
        {
            // Sanity check to make sure we haven't already run this query
            if (HasExecuted)
            {
                throw new InvalidOperationException("Query has already executed.");
            }

            // Create a connection from the connection details
            string connectionString = ConnectionService.BuildConnectionString(EditorConnection.ConnectionDetails);
            using (DbConnection conn = EditorConnection.Factory.CreateSqlConnection(connectionString))
            {
                await conn.OpenAsync(cancellationSource.Token);

                // Create a command that we'll use for executing the query
                using (DbCommand command = conn.CreateCommand())
                {
                    command.CommandText = QueryText;
                    command.CommandType = CommandType.Text;

                    // Execute the command to get back a reader
                    using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationSource.Token))
                    {
                        do
                        {
                            // Create a new result set that we'll use to store all the data
                            ResultSet resultSet = new ResultSet();
                            if (reader.CanGetColumnSchema())
                            {
                                resultSet.Columns = reader.GetColumnSchema().ToArray();
                            }

                            // Read until we hit the end of the result set
                            while (await reader.ReadAsync(cancellationSource.Token))
                            {
                                resultSet.AddRow(reader);
                            }

                            // Add the result set to the results of the query
                            ResultSets.Add(resultSet);
                        } while (await reader.NextResultAsync(cancellationSource.Token));
                    }
                }
            }

            // Mark that we have executed
            HasExecuted = true;
        }

        public ResultSetSubset GetSubset(int resultSetIndex, int startRow, int rowCount)
        {
            // Sanity check that the results are available
            if (!HasExecuted)
            {
                throw new InvalidOperationException("The query has not completed, yet.");
            }

            // Sanity check to make sure we have valid numbers
            if (resultSetIndex < 0 || resultSetIndex >= ResultSets.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(resultSetIndex), "Result set index cannot be less than 0" +
                                                                             "or greater than the number of result sets");
            }
            ResultSet targetResultSet = ResultSets[resultSetIndex];
            if (startRow < 0 || startRow >= targetResultSet.Rows.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow), "Start row cannot be less than 0 " +
                                                                        "or greater than the number of rows in the resultset");
            }
            if (rowCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowCount), "Row count must be a positive integer");
            }

            // Retrieve the subset of the results as per the request
            object[][] rows = targetResultSet.Rows.Skip(startRow).Take(rowCount).ToArray();
            return new ResultSetSubset
            {
                Rows = rows,
                RowCount = rows.Length
            };
        }
    }
}

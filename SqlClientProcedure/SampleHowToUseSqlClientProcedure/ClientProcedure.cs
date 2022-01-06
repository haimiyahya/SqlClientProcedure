//  Copyright(c) 2022 Mohd Norhaimi bin Yahya

//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.IO;

namespace SqlClientProcedure
{
    public record Procedure
    {
        public string? Database { get; init; }
        public string? Name { get; init; }
        public SqlParameter[]? Params { get; init; }
        public string? Text { get; init; }
    }

    public static class ClientProcedure
    {
        private static Dictionary<string, Procedure> m_aoProcedures = new();

        /// <summary>
        /// Load all procedure located in embedded resources
        /// </summary>
        /// <param name="assemblyNameSpace"></param>
        /// <param name="sqlDirectory"></param>
        public static void InitClientProcedure(string sqlDirectory)
        {
            // the resource directory structure:
            // sp / database / sproc.sql
            // for example for database with name TestDB and procedure name AddUser which stored in directory procedure, the structure is:
            // procedure/TestDB/AddUser.sql

            string[] listOfProcedure = Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(r => Regex.IsMatch(r, $".*{sqlDirectory}") && r.EndsWith(".sql"))
            .ToArray();

            for (int i = 0; i < listOfProcedure.Length; i++)
            {
                var databaseName = Regex.Match(listOfProcedure[i], @"(?<database>[^\.]+)\.\w+\.sql$").Groups["database"].Value;

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(listOfProcedure[i]))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            string procedureContent = reader.ReadToEnd();

                            var oMatchResult = Regex.Match(procedureContent, @"create\s+procedure\s+(\[*dbo\]*\.)?\[*(?<proc_name>\w+)\]*\s*(?<proc_params>.*\s)as\s*(?<proc_body>.*)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                            var procedureName = oMatchResult.Groups["proc_name"].Value;
                            var procedureParams = oMatchResult.Groups["proc_params"].Value;
                            var procedureBody = oMatchResult.Groups["proc_body"].Value;

                            var sp_param_list = Regex.Matches(procedureParams, @"(?<param_name>\@\w+)\s+(?<type>.*)");

                            var aoParams = new SqlParameter[sp_param_list.Count];
                            for (int j = 0; j < sp_param_list.Count; j++)
                            {
                                var paramName = sp_param_list[j].Groups["param_name"].Value;
                                var paramType = sp_param_list[j].Groups["type"].Value;

                                var oMatchResultType = Regex.Match(paramType, @"(?<type_name>\w+)\s*(\((?<size>\d+)(\s*\,\*(?<precision>\d+)\s*)?\))?");

                                var oParamDataType = oMatchResultType.Groups["type_name"].Value;
                                var oParamSize = oMatchResultType.Groups["size"].Value;
                                var oParamPrecision = oMatchResultType.Groups["precision"].Value;

                                SqlDbType oType = oParamDataType switch
                                {
                                    "varchar" or "char" => SqlDbType.VarChar,
                                    "nvarchar" or "nchar" => SqlDbType.NVarChar,
                                    "int" => SqlDbType.Int,
                                    "decimal" => SqlDbType.Decimal,
                                    "float" => SqlDbType.Float,
                                    _ => SqlDbType.NVarChar,
                                };

                                oParamSize = oParamSize ?? "0";
                                Int32.TryParse(oParamSize.ToString(), out int paramSize);

                                oParamPrecision = oParamPrecision ?? "0";
                                Int32.TryParse(oParamPrecision.ToString(), out int iParamPrecision);
                                byte paramPrecision = (byte)iParamPrecision;

                                SqlParameter oParam = (paramSize, paramPrecision) switch
                                {
                                    (0, 0) => new SqlParameter(paramName, oType), // for example: int
                                    (_, 0) => new SqlParameter(paramName, oType, paramSize), // for example: varchar(10)
                                    (_, _) => new SqlParameter() { ParameterName = paramName, SqlDbType = oType, Size = paramSize, Precision = paramPrecision }, // for example: decimal(18,2)
                                };

                                aoParams[j] = oParam;
                            }

                            var oProc = new Procedure
                            {
                                Database = databaseName,
                                Name = procedureName,
                                Params = aoParams,
                                Text = procedureBody
                            };

                            m_aoProcedures.Add(databaseName + "." + oProc.Name, oProc);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lookup the listing of client procedure loaded during init
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="spName"></param>
        /// <returns></returns>
        public static Procedure? GetProcedure(string databaseName, string procedureName)
        {
            databaseName = databaseName.Replace("-", "_"); // dash in manifest name usually will be replaced with underscore

            return m_aoProcedures?.GetValueOrDefault(databaseName + "." + procedureName);
        }

        /// <summary>
        /// replace the CommandText with the client procedure text 
        /// and update the parameter type according to the defined type in the sql file
        /// </summary>
        /// <param name="oProc"></param>
        /// <param name="cmd"></param>
        public static void UpdateParamDataTypeAndProcedureText(Procedure oProc, SqlCommand cmd) // parameter to be updated
        {
            foreach (SqlParameter sqlParam in cmd.Parameters)
            {
                var oParam = oProc.Params?.FirstOrDefault<SqlParameter>(x => x.ParameterName == sqlParam.ParameterName);
                sqlParam.SqlDbType = oParam?.SqlDbType ?? SqlDbType.VarChar;
            }

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = oProc.Text;
        }
    }

    public static class SqlCommandExtension
    {
        /// <summary>
        /// Lookup the client procedure (sql file) by using database name and procedure name, throw exception if not exists
        /// If client procedure exists, replace the CommandText with the client procedure text 
        /// and update the parameter type according to the defined type in the sql file
        /// </summary>
        /// <param name="cmd"></param>
        /// <exception cref="Exception"></exception>
        public static void UseClientProcedure(this SqlCommand cmd)
        {
            string databaseName = cmd.Connection.Database;
            string procedureName = cmd.CommandText;

            Procedure? oProc = ClientProcedure.GetProcedure(databaseName, procedureName);

            ClientProcedure.UpdateParamDataTypeAndProcedureText(oProc ?? throw new Exception($"Client Stored Procedure {databaseName}.{procedureName} was not found"),
                cmd);
        }
    }
}
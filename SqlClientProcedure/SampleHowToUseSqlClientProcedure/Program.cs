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

using System.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SqlClientProcedure; // copy the ClientProcedure.cs file to the project then add using clause

// for long running application (web app or service), place this in the app/service startup
// this is the part where ClientProcedure loads the procedure text files
ClientProcedure.InitClientProcedure("sp"); // sp is the directory name of the procedure files (make sure all the file included as embedded resource)
// to include entire directory as embedded resouce, add node similar to this in the csproj file:
// <ItemGroup>   < EmbeddedResource Include = "sp\UA-CMS\*.sql" />    </ ItemGroup >

 var connectionString = @"Server=(localdb)\MSSQLLocalDB;Initial Catalog=UA-CMS;Integrated Security=True";

// for this sample, create database UA-CMS then run this sql to create the table
// create table CMS_User (UserID varchar(50), Pwd varchar(50));


// assuming this is the existing code in your system
using (SqlConnection connection = new SqlConnection(connectionString))
{
    string sProcName = "cms_AddUser";
    SqlCommand command = new SqlCommand(sProcName, connection);

    connection.Open();
    connection.ChangeDatabase("UA-CMS");

    command.Parameters.AddWithValue("@s_UserID", "test_user_4");
    command.Parameters.AddWithValue("@s_Pwd", "12345");

    command.UseClientProcedure(); // you just need to invoke "UseClientStoredProcedure" extension method

    var oReader = command.ExecuteReader();
    command.Parameters.Clear();

    oReader.Read();

    Console.WriteLine(oReader["AffectedRows"].ToString());
}
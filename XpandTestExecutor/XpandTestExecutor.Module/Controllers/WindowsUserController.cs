using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Xpand.Persistent.Base.General;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers {
    public class WindowsUserController : ObjectViewController<ObjectView, WindowsUser> {
        private const string DropDB = "DropDB";
        private const string CreateUser = "CreateUser";
        private const string DropDBUser = "DropDBUser";
        public WindowsUserController() {
            var singleChoiceAction = new SingleChoiceAction(this, "UserAction", PredefinedCategory.ObjectsCreation) {
                ItemType = SingleChoiceActionItemType.ItemIsOperation
            };
            singleChoiceAction.Items.Add(new ChoiceActionItem(DropDB, DropDB));
            singleChoiceAction.Items.Add(new ChoiceActionItem(CreateUser, CreateUser));
            singleChoiceAction.Items.Add(new ChoiceActionItem(DropDBUser, DropDBUser));
            singleChoiceAction.Execute += DropDBOnExecute;
        }

        private void DropDBOnExecute(object sender, SingleChoiceActionExecuteEventArgs e) {
            var dbConnection = (SqlConnection)ObjectSpace.Session().Connection;
            if ((string)e.SelectedChoiceActionItem.Data == DropDBUser) {
                var logins = GetLogins(dbConnection);
                var sqlCommand = dbConnection.CreateCommand();
                foreach (var login in logins) {
                    sqlCommand.CommandText = "Drop login [" + login + "]";
                    sqlCommand.ExecuteNonQuery();
                }
            } else if ((string)e.SelectedChoiceActionItem.Data == CreateUser) {
                foreach (WindowsUser user in e.SelectedObjects.Cast<WindowsUser>()) {
                    using (SqlCommand sqlCommand = dbConnection.CreateCommand()) {
                        string userName = Environment.MachineName + @"\" + user.Name;
                        sqlCommand.CommandText = "CREATE LOGIN [" + userName + "] FROM WINDOWS WITH DEFAULT_DATABASE=[master]";
                        sqlCommand.ExecuteNonQuery();
                        sqlCommand.CommandText = "ALTER SERVER ROLE [sysadmin] ADD MEMBER [" + userName + "]";
                        sqlCommand.ExecuteNonQuery();
                    }
                }

            } else if ((string)e.SelectedChoiceActionItem.Data == DropDB) {
                DataTable databases = dbConnection.GetSchema("Databases");
                foreach (DataRow database in databases.Rows) {
                    var databaseName = (string)database["database_name"];
                    if (
                        e.SelectedObjects.Cast<WindowsUser>()
                            .Any(user => databaseName.ToLower().EndsWith("_" + user.Name.ToLower()))) {
                        using (var sqlCommand = dbConnection.CreateCommand()) {
                            sqlCommand.CommandText = "DROP database [" + databaseName + "]";
                            sqlCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private IEnumerable<string> GetLogins(SqlConnection dbConnection) {
            var logins = new List<string>();
            using (var sqlCommand = dbConnection.CreateCommand()) {
                sqlCommand.CommandText =
                    "SELECT name AS Login_Name, type_desc AS Account_Type FROM sys.server_principals WHERE TYPE IN ('U') and name not like '%##%' And Name like '%EasyTest1' ORDER BY name, type_desc";
                using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader()) {
                    while (sqlDataReader.Read()) {
                        logins.Add((string)sqlDataReader["Login_name"]);
                    }
                }
            }
            return logins;
        }
    }
}

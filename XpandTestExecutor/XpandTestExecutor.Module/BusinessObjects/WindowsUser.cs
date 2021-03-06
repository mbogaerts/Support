using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using Microsoft.Win32;

namespace XpandTestExecutor.Module.BusinessObjects {
    [DefaultClassOptions]
    [FriendlyKeyProperty("Name")]
    [DefaultProperty("Name")]
    public class WindowsUser : BaseObject {
        private const string RegistryPath = @"Software\Xpand\ProcessAsUser";
        public WindowsUser(Session session)
            : base(session) {
        }

        [Association("ExecutionInfos-Users")]
        public XPCollection<ExecutionInfo> ExecutionInfos {
            get { return GetCollection<ExecutionInfo>("ExecutionInfos"); }
        }
        [RuleUniqueValue]
        public string Name { get; set; }


        public string Password { get; set; }

        public static string Domain {
            get{
                var registryKey = Registry.LocalMachine.CreateSubKey(RegistryPath);
                return registryKey != null ? registryKey.GetValue("Domain", null) as string : null;
            }
        }

        public static IEnumerable<WindowsUser> CreateUsers(UnitOfWork unitOfWork, bool rdc) {
            if (rdc) {
                RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(RegistryPath);
                if (registryKey != null) {
                    var userNames = (string)registryKey.GetValue("UserName", "");
                    if (!string.IsNullOrEmpty(userNames)) {
                        return CreateUser(registryKey, userNames, unitOfWork);
                    }
                }
                throw new NotImplementedException();
            }
            return new[] { CreateUser(unitOfWork, null) };
        }
        private static IEnumerable<WindowsUser> CreateUser(RegistryKey registryKey, string userNames, UnitOfWork unitOfWork) {
            string[] passwords = ((string)registryKey.GetValue("Password")).Split(';');
            for (int i = 0; i < userNames.Split(';').Length; i++) {
                string userName = userNames.Split(';')[i];
                if (!string.IsNullOrEmpty(userName)) {
                    yield return CreateUser(unitOfWork, userName, passwords[i]);
                }
            }

            unitOfWork.ValidateAndCommitChanges();
        }

        private static WindowsUser CreateUser(UnitOfWork unitOfWork, string userName, string password = null) {
            var user = new XPQuery<WindowsUser>(unitOfWork).FirstOrDefault(windowsUser => windowsUser.Name == userName) ??
                       new WindowsUser(unitOfWork);
            user.Name = userName;
            user.Password = password;
            return user;
        }
    }
}
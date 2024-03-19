using Avanpost.Interviews.Task.Integration.Data.DbCommon;
using Avanpost.Interviews.Task.Integration.Data.DbCommon.DbModels;
using Avanpost.Interviews.Task.Integration.Data.Models;
using Avanpost.Interviews.Task.Integration.Data.Models.Models;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;


namespace Avanpost.Interviews.Task.Integration.SandBox.Connector
{
    public class ConnectorDb : IConnector
    {
        private DataContext dataContext;
        public ILogger Logger { get; set; }
        public void StartUp(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
            DbContextOptions<DataContext> options;
            var connectionStringBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
            string? conString = connectionStringBuilder["ConnectionString"] as string;
            string? providerName = connectionStringBuilder["Provider"] as string;
            if (providerName!.Contains("SqlServer"))
            {
                options = optionsBuilder.UseSqlServer(conString).Options;
            }
            dataContext = new DataContext(optionsBuilder.Options);
        }

        public void CreateUser(UserToCreate user)
        {
            User userEntity = new User()
            {
                Login = user.Login,
                LastName = user.Properties.SingleOrDefault(x => x.Name == nameof(userEntity.LastName))?.Value ?? string.Empty,
                FirstName = user.Properties.SingleOrDefault(x => x.Name == nameof(userEntity.FirstName))?.Value ?? string.Empty,
                MiddleName = user.Properties.SingleOrDefault(x => x.Name == nameof(userEntity.MiddleName))?.Value ?? string.Empty,
                TelephoneNumber = user.Properties.SingleOrDefault(x => x.Name == nameof(userEntity.TelephoneNumber))?.Value ?? string.Empty,
                IsLead = Convert.ToBoolean(user.Properties.SingleOrDefault(x => x.Name == nameof(userEntity.IsLead))?.Value)
            };
            dataContext.Users.Add(userEntity);
            Sequrity sequrityEntity = new Sequrity()
            {
                UserId = user.Login,
                Password = user.HashPassword
            };
            dataContext.Passwords.Add(sequrityEntity);
            dataContext.SaveChanges();
        }

        public IEnumerable<Property> GetAllProperties()
        {
            var propertys = dataContext.Users.EntityType.GetProperties().Where(x => !x.IsKey()).
            Select(x => new Property(x.Name, string.Empty)).ToList();
            return propertys;
        }

        public IEnumerable<UserProperty> GetUserProperties(string userLogin)
        {
            User user = dataContext.Users.FirstOrDefault(x => x.Login == userLogin)!;
            Sequrity sequrity = dataContext.Passwords.FirstOrDefault(x => x.UserId == userLogin)!;
            List<UserProperty> properties = new List<UserProperty>()
                {
                    new("firstName", user.FirstName),
                    new("middleName", user.MiddleName),
                    new("lastName", user.LastName),
                    new("telephoneNumber", user.TelephoneNumber),
                    new("password", sequrity.Password)
                };
            return properties;
        }

        public bool IsUserExists(string userLogin)
        {
            bool isUserExist = dataContext.Users.Any(u => u.Login == userLogin);
            return isUserExist;
        }

        public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
        {
            try
            {
                User user = dataContext.Users.FirstOrDefault(x => x.Login == userLogin)!;
                var userAttributes = dataContext.Users.EntityType.GetProperties();
                foreach (UserProperty userProperty in properties)
                {
                    if (userProperty.Name != "password")
                        dataContext.Entry(user).Property(userAttributes.FirstOrDefault(x => x.Name.ToLower() == userProperty.Name.ToLower())!)
                            .CurrentValue = userProperty.Value;
                    else if (userProperty.Name == "password")
                    {
                        dataContext.Passwords.FirstOrDefault(x => x.UserId == userLogin)!.Password = userProperty.Value;
                    }
                }
                dataContext.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                throw;
            }

        }

        public IEnumerable<Permission> GetAllPermissions()
        {

            var requestRights = dataContext.RequestRights.Select(x => new Permission(x.Id.ToString()!, x.Name, string.Empty));
            var iTRoles = dataContext.ITRoles.Select(x => new Permission(x.Id.ToString()!, x.Name, string.Empty));
            return new[]
            {
                    requestRights,iTRoles
                }.SelectMany(x => x);
        }

        public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            foreach (string rightId in rightIds)
            {
                switch (DetectedPermissionRigh(rightId))
                {
                    case "Role":
                        dataContext.UserITRoles.Add(new UserITRole()
                        {
                            UserId = userLogin,
                            RoleId = DetectedPermissionID(rightId)
                        });
                        break;
                    case "Request":
                        dataContext.UserRequestRights.Add(new UserRequestRight()
                        {
                            UserId = userLogin,
                            RightId = DetectedPermissionID(rightId)
                        });
                        break;
                }

            }
            dataContext.SaveChanges();
        }
        public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            foreach (string rightId in rightIds)
            {
                switch (DetectedPermissionRigh(rightId))
                {
                    case "Role":
                        UserITRole userITRole = dataContext.UserITRoles.FirstOrDefault(x => x.UserId == userLogin &&
                        x.RoleId == DetectedPermissionID(rightId))!;
                        dataContext.UserITRoles.Remove(userITRole!);
                        break;
                    case "Request":
                        UserRequestRight userRequestRight = dataContext.UserRequestRights.FirstOrDefault(x => x.UserId == userLogin &&
                        x.RightId == DetectedPermissionID(rightId))!;
                        dataContext.UserRequestRights.Remove(userRequestRight!);
                        break;
                }
            }
            dataContext.SaveChanges();
        }

        public IEnumerable<string> GetUserPermissions(string userLogin)
        {
            var userItRole = dataContext.UserITRoles
                .Where(x => x.UserId == userLogin)
                .Join(dataContext.ITRoles,
                outer => outer.RoleId,
                inner => inner.Id,
                (outer, inner) => inner.Name);

            var userRequestRight = dataContext.UserRequestRights
                .Where(x => x.UserId == userLogin)
                .Join(dataContext.RequestRights,
                outer => outer.RightId,
                inner => inner.Id,
                (outer, inner) => inner.Name);


            return new[] {
                userItRole,
                userRequestRight}
            .SelectMany(x => x);
        }

        private string DetectedPermissionRigh(string rightId)
        {
            return rightId.Split(':')[0];
        }
        private int DetectedPermissionID(string rightId)
        {
            return Convert.ToInt32(rightId.Split(':')[1]);
        }
    }
}
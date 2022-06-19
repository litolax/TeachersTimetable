using MongoDB.Driver;
using TeachersTimetable.Config;

namespace TeachersTimetable.Services
{
    public interface IMongoService
    {
        IMongoDatabase Database { get; set; }
    }

    public class MongoService : IMongoService
    {
        public string TableDBName;

        private static MongoClientSettings Settings;

        public MongoClient Client;
        public IMongoDatabase Database { get; set; }

        public MongoService()
        {
            this.GetSettings();
        }

        public void GetSettings()
        {
            var mongoConfig = new Config<MongoConfig>();
#if !DEBUG
            this.TableDBName = mongoConfig.Entries.DbName;
            Settings = new()
            {
                Server = new MongoServerAddress(mongoConfig.Entries.Host, mongoConfig.Entries.Port),
                Credential = MongoCredential.CreateCredential(mongoConfig.Entries.DbName,
                    mongoConfig.Entries.AuthorizationName, mongoConfig.Entries.AuthorizationPassword)
            };
            Client = new(Settings);
            Database = Client.GetDatabase(TableDBName);
#endif
         
#if DEBUG
            this.TableDBName = "Teachers-Timetable";
            Client = new("mongodb://localhost:27017/?readPreference=primary&appname=MongoDB%20Compass&directConnection=true&ssl=false");
            Database = Client.GetDatabase(TableDBName);
#endif
        }
    }
}
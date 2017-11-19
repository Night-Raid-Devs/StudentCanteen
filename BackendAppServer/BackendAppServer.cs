using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using BackendCommon;
using BackendDatabase;

namespace BackendAppServer
{
    public partial class BackendAppServer : ServiceBase
    {
        private WCFServiceHost host = null;
        private int stopThreadSleep = 5000;     // Stop thread sleep in ms

        public BackendAppServer()
        {
            this.InitializeComponent();
        }

        public static void Main(string[] args)
        {
            BackendAppServer service = new BackendAppServer();

            if (Environment.UserInteractive)
            {
                service.OnStart(args);
                Console.WriteLine("Starting from console.\nPress any key to stop");
                Console.Read();
                service.OnStop();
            }
            else
            {
                ServiceBase.Run(service);
            }
        }

        protected override void OnStart(string[] args)
        {
            ConnectionData data = new ConnectionData();

            try
            {
                string configFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "BackendAppServer.xml");
                bool found = false;

                StreamReader streamReader = new StreamReader(configFile);
                XmlReader reader = XmlReader.Create(streamReader);
                XmlSerializer serializer = new XmlSerializer(typeof(ConnectionData));

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == typeof(ConnectionData).Name)
                    {
                        data = (ConnectionData)serializer.Deserialize(reader.ReadSubtree());
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new Exception(string.Format("Could not find configuration for {0} in BackendAppServer.xml", typeof(ConnectionData).Name));
                }

                DatabaseManager.Instance.Initialize(data);

                this.host = new WCFServiceHost(typeof(RestApiService));
                this.host.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in OnStart: {0}", e.Message);
                this.Stop();
            }
        }

        protected override void OnStop()
        {
            if (this.host != null)
            {
                try
                {
                    this.host.Close();
                }
                catch
                {
                }

                this.host = null;

                // Let to finish all DB tasks
                Thread.Sleep(this.stopThreadSleep);
            }
        }
    }
}

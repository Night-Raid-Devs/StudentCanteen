using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Configuration;

namespace BackendAppServer
{
    public class WCFServiceHost : ServiceHost
    {
        public WCFServiceHost(Type serviceType, params Uri[] baseAddresses)
            : base(serviceType, baseAddresses)
        {
        }

        protected override void ApplyConfiguration()
        {
            string configFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "BackendAppServer.xml");

            if (File.Exists(configFile) == false)
            {
                throw new Exception(string.Format("The XML configuration file has not been found: {0}", configFile));
            }
            else
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = configFile;
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                ServiceModelSectionGroup serviceModel = ServiceModelSectionGroup.GetSectionGroup(config);
                bool initialized = false;

                foreach (ServiceElement elem in serviceModel.Services.Services)
                {
                    if (elem.Name == Description.ConfigurationName)
                    {
                        this.LoadConfigurationSection(elem);
                        initialized = true;
                        break;
                    }
                }

                if (!initialized)
                {
                    throw new Exception(string.Format("Could not find configuration for {0} in BackendAppServer.xml", Description.ConfigurationName));
                }
            }
        }
    }
}

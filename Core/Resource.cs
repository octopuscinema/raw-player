using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Octopus.Player.Core
{
    public static class Resource
    {
        public static string LoadAsciiResource(string resourceFileName, Assembly assembly = null)
        {
            // Default assembly is local assembly (Player.Core)
            if (assembly == null)
                assembly = Assembly.GetExecutingAssembly();

            var resources = assembly.GetManifestResourceNames();
            foreach (string resource in resources)
            {
                if (resource.Contains(resourceFileName))
                {
                    var resourceSourceStream = assembly.GetManifestResourceStream(resource);
                    StreamReader resourceReader = new StreamReader(resourceSourceStream);
                    var resourceAscii = resourceReader.ReadToEnd();
                    resourceReader.Dispose();
                    resourceSourceStream.Dispose();
                    return resourceAscii;
                }
            }

            return null;
        }
    }
}

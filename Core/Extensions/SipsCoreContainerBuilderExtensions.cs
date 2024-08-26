using Autofac;
using SIPS.Framework.Core.AutoRegister.Extensions;
using System;

namespace SIPS.Framework.Core.Extensions
{
    public static class SipsCoreContainerBuilderExtensions
    {
        private static bool _isRegistered = false;

        public static ContainerBuilder RegisterCoreProviders(this ContainerBuilder containerBuilder)
        {
            if (_isRegistered)
            {
                return containerBuilder;
            }
            if (containerBuilder == null)
            {
                throw new ArgumentNullException("containerBuilder");
            }

            var assembly = typeof(SipsCoreContainerBuilderExtensions).Assembly;
            SIPSRegistrationToolbox.ConfigureContainer(containerBuilder, assembly, "Core");
            // SIPSRegistrationToolbox.ConfigureContainerNamed<xxxx>(containerBuilder, assembly, "SDA");

            _isRegistered = true;
            return containerBuilder;
        }
    }
}

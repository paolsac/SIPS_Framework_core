using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIPS.Framework.Core.AutoRegister.Constants;
using SIPS.Framework.Core.AutoRegister.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SIPS.Framework.Core.AutoRegister.Extensions
{
    public static class SIPSRegistrationToolbox
    {
        public const string SIPS_Registration_Config = "SIPS_Registration_Config";
        public const string SIPS_Registration_Logger = "SIPS_Registration_Logger";
        private static readonly Dictionary<string, List<string>> _registrations = new Dictionary<string, List<string>>();
        private static bool dump;

        public static Dictionary<string, List<string>> Registrations { get => _registrations; }
        public static bool Dump { get => dump; set => dump = value; }

        public static IServiceCollection AddAutoRegistrations(this IServiceCollection services, IConfiguration configuration, Assembly assembly)
        {
            var assembly1 = typeof(ServiceCollectionHostedServiceExtensions).Assembly;
            MethodInfo method = null;

            Dump = configuration.GetValue<bool>($"{ConfigConstants.AutoRegister_ConfigurationFullSectionName}:AutoRegister:Options:LogListOfRegisteredClasses", true);
            foreach (MethodInfo mi in GetExtensionMethods(assembly1))
            {
                if (mi.Name != "AddHostedService")
                    continue;
                method = mi;
                break;
            }

            IEnumerable<Type> types = assembly.GetTypes().Where(t => typeof(IFCAutoRegisterHostedService).IsAssignableFrom(t));
            foreach (var item in types)
            {
                MethodInfo generic = method.MakeGenericMethod(item);
                generic.Invoke(null, new object[] { services });
                AddRegistration("IFCAutoRegisterHostedService", item.FullName);
            }

            return services;
        }

        private static void AddRegistration(string key, string value)
        {
            List<string> list;
            if (!_registrations.TryGetValue(key, out list))
            {
                list = new List<string>();
                _registrations.Add(key, list);
            }
            list.Add(value);
        }

        private static IEnumerable<MethodInfo> GetExtensionMethods(Assembly assembly) //, Type extendedType)
        {
            var query = from type in assembly.GetTypes()
                        where type.IsSealed && !type.IsGenericType && !type.IsNested
                        from method in type.GetMethods(BindingFlags.Static
                            | BindingFlags.Public | BindingFlags.NonPublic)
                        where method.IsDefined(typeof(ExtensionAttribute), false)
                        // where method.GetParameters()[0].ParameterType == extendedType
                        select method;
            return query;
        }

        public static void ConfigureContainer(ContainerBuilder builder, Assembly serviceAssembly, string autoRegisterConfigKey)
        {
            bool original_dump = SIPSRegistrationToolbox.Dump;
            IEnumerable<Type> types;

            IConfiguration config = null;
            if (builder.Properties.ContainsKey(SIPSRegistrationToolbox.SIPS_Registration_Config))
            {
                config = builder.Properties[SIPSRegistrationToolbox.SIPS_Registration_Config] as IConfiguration;
            }
            Serilog.ILogger logger = null;
            if (builder.Properties.ContainsKey(SIPSRegistrationToolbox.SIPS_Registration_Logger))
            {
                logger = builder.Properties[SIPSRegistrationToolbox.SIPS_Registration_Logger] as Serilog.ILogger;
            }

            if (config != null)
            {
                var section = config.GetSection($"{ConfigConstants.FC_ConfigurationRootSectionName}{autoRegisterConfigKey}:AutoRegister");
                if (section != null)
                {
                    SIPSRegistrationToolbox.Dump = section.GetValue<bool>("Options:LogListOfRegisteredClasses", false);
                }
            }

            //
            // IFCAutoRegisterSingleton
            //
            {
                types = serviceAssembly.GetTypes().Where(t => typeof(IFCAutoRegisterSingleton).IsAssignableFrom(t));

                foreach (var item in types)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterSingleton: {provider}", item.Name);
                    AddRegistration("IFCAutoRegisterSingleton", item.FullName);
                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                    .Where(t => typeof(IFCAutoRegisterSingleton).IsAssignableFrom(t))
                    .SingleInstance();
            }

            //
            // IFCAutoRegisterTransient
            //
            {
                types = serviceAssembly.GetTypes().Where(t => typeof(IFCAutoRegisterTransient).IsAssignableFrom(t));
                foreach (var item in types)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterTransient: {provider}", item.Name);
                    AddRegistration("IFCAutoRegisterTransient", item.FullName);

                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                    .Where(t => typeof(IFCAutoRegisterTransient).IsAssignableFrom(t))
                    .InstancePerDependency();
            }

            //
            // IFCAutoRegisterScoped
            //
            {
                types = serviceAssembly.GetTypes().Where(t => typeof(IFCAutoRegisterScoped).IsAssignableFrom(t));
                foreach (var item in types)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterScoped: {provider}", item.Name);
                    AddRegistration("IFCAutoRegisterScoped", item.FullName);

                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                    .Where(t => typeof(IFCAutoRegisterScoped).IsAssignableFrom(t))
                    .InstancePerLifetimeScope();
            }

            ////
            //// IIntegrationEventHandler
            ////
            //{
            //    types = serviceAssembly.GetTypes().Where(t => typeof(IIntegrationEventHandler).IsAssignableFrom(t));
            //    foreach (var item in types)
            //    {
            //        if (Dump)
            //            Debug.WriteLine($"IIntegrationEventHandler: {item.Name}");
            //        AddRegistration("IIntegrationEventHandler", item.FullName);
            //    }
            //    builder.RegisterAssemblyTypes(serviceAssembly)
            //        .Where(t => typeof(IIntegrationEventHandler).IsAssignableFrom(t))
            //        .InstancePerDependency();
            //}


            SIPSRegistrationToolbox.Dump = original_dump;
        }

        public static void ConfigureContainerNamed<TService>(ContainerBuilder builder, Assembly serviceAssembly, string autoRegisterConfigKey, string customName = null)
        {
            bool original_dump = SIPSRegistrationToolbox.Dump;
            IEnumerable<Type> namedTypes;

            IConfiguration config = null;
            if (builder.Properties.ContainsKey(SIPSRegistrationToolbox.SIPS_Registration_Config))
            {
                config = builder.Properties[SIPSRegistrationToolbox.SIPS_Registration_Config] as IConfiguration;
            }
            Serilog.ILogger logger = null;
            if (builder.Properties.ContainsKey(SIPSRegistrationToolbox.SIPS_Registration_Logger))
            {
                logger = builder.Properties[SIPSRegistrationToolbox.SIPS_Registration_Logger] as Serilog.ILogger;
            }

            if (config != null)
            {
                var section = config.GetSection($"{ConfigConstants.FC_ConfigurationRootSectionName}{autoRegisterConfigKey}:AutoRegister");
                if (section != null)
                {
                    SIPSRegistrationToolbox.Dump = section.GetValue<bool>("Options:LogListOfRegisteredClasses", true);
                }
            }

            //
            // IFCAutoRegisterScoped
            //
            {
                namedTypes = serviceAssembly.GetTypes().Where(t => typeof(IFCAutoRegisterScopedNamed).IsAssignableFrom(t));
                foreach (var item in namedTypes)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterScopedNamed: {provider}", item.Name);
                    AddRegistration("[named]-IFCAutoRegisterScoped", item.FullName);

                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                    .Where(t => typeof(IFCAutoRegisterScopedNamed).IsAssignableFrom(t))
                    .InstancePerLifetimeScope()
                    .Named<TService>(t => customName ?? t.Name)
                    ;
            }
            //
            // IFCAutoRegisterTransientNamed
            //
            {
                namedTypes = serviceAssembly.GetTypes()
                    .Where(t =>
                        typeof(IFCAutoRegisterTransientNamed).IsAssignableFrom(t)
                        );
                foreach (var item in namedTypes)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterTransientNamed: {provider}", item.Name);
                    AddRegistration("[named]-IFCAutoRegisterTransient", item.FullName);

                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                    .Where(t => typeof(IFCAutoRegisterTransientNamed).IsAssignableFrom(t))
                    .InstancePerDependency()
                    .Named<TService>(t => customName ?? t.Name)
                    ;
            }

        }

        public static void ConfigureContainerNamedByInterface<TInterface, TService>(ContainerBuilder builder, Assembly serviceAssembly, string autoRegisterConfigKey, string customName = null)
        {
            bool original_dump = SIPSRegistrationToolbox.Dump;
            IEnumerable<Type> namedTypes;

            IConfiguration config = null;
            if (builder.Properties.ContainsKey(SIPSRegistrationToolbox.SIPS_Registration_Config))
            {
                config = builder.Properties[SIPSRegistrationToolbox.SIPS_Registration_Config] as IConfiguration;
            }
            Serilog.ILogger logger = null;
            if (builder.Properties.ContainsKey(SIPSRegistrationToolbox.SIPS_Registration_Logger))
            {
                logger = builder.Properties[SIPSRegistrationToolbox.SIPS_Registration_Logger] as Serilog.ILogger;
            }

            if (config != null)
            {
                var section = config.GetSection($"{ConfigConstants.FC_ConfigurationRootSectionName}{autoRegisterConfigKey}:AutoRegister");
                if (section != null)
                {
                    SIPSRegistrationToolbox.Dump = section.GetValue<bool>("Options:LogListOfRegisteredClasses", true);
                }
            }

            //
            // IFCAutoRegisterScoped
            //
            {
                namedTypes = serviceAssembly.GetTypes()
                    .Where(t => 
                        typeof(IFCAutoRegisterScopedNamed).IsAssignableFrom(t)
                        && typeof(TInterface).IsAssignableFrom(t)
                        );
                foreach (var item in namedTypes)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterScopedNamed: {provider}", item.Name);
                    AddRegistration("[named]-IFCAutoRegisterScoped", item.FullName);

                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                    .Where(t => 
                        typeof(IFCAutoRegisterScopedNamed).IsAssignableFrom(t)
                        && typeof(TInterface).IsAssignableFrom(t)
                        )
                    .InstancePerLifetimeScope()
                    .Named<TInterface>(t => customName ?? t.Name)
                    ;
            }
            //
            // IFCAutoRegisterTransientNamed
            //
            {
                namedTypes = serviceAssembly.GetTypes()
                    .Where(t =>
                        typeof(IFCAutoRegisterTransientNamed).IsAssignableFrom(t)
                        && typeof(TInterface).IsAssignableFrom(t)
                        );
                foreach (var item in namedTypes)
                {
                    if (Dump && logger != null)
                        logger.Debug("IFCAutoRegisterTransientNamed: {provider}", item.Name);
                    AddRegistration("[named]-IFCAutoRegisterTransient", item.FullName);

                }
                builder.RegisterAssemblyTypes(serviceAssembly)
                     .Where(t =>
                        typeof(IFCAutoRegisterTransientNamed).IsAssignableFrom(t)
                        && typeof(TInterface).IsAssignableFrom(t)
                        )
                    .InstancePerDependency()
                    .Named<TInterface>(t => customName ?? t.Name)
                    ;
            }

        }

        public static void ConfigureContainer(ContainerBuilder builder)
        {
            var sharedServicesAssembly = Assembly.GetExecutingAssembly();
            ConfigureContainer(builder, sharedServicesAssembly, "AutoRegister");
        }
    }
}

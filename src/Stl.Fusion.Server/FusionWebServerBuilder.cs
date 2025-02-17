using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.Collections;
using Stl.Fusion.Authentication;
using Stl.Fusion.Server.Controllers;
using Stl.Fusion.Server.Internal;
using Stl.Text;
using Stl.Time;

namespace Stl.Fusion.Server
{
    public readonly struct FusionWebServerBuilder
    {
        private class AddedTag { }
        private static readonly ServiceDescriptor AddedTagDescriptor =
            new(typeof(AddedTag), new AddedTag());

        public FusionBuilder Fusion { get; }
        public IServiceCollection Services => Fusion.Services;

        internal FusionWebServerBuilder(FusionBuilder fusion,
            Action<IServiceProvider, WebSocketServer.Options>? webSocketServerOptionsBuilder)
        {
            Fusion = fusion;
            if (Services.Contains(AddedTagDescriptor))
                return;
            // We want above Contains call to run in O(1), so...
            Services.Insert(0, AddedTagDescriptor);

            Fusion.AddPublisher();
            Services.TryAddSingleton(c => {
                var options = new WebSocketServer.Options();
                webSocketServerOptionsBuilder?.Invoke(c, options);
                return options;
            });
            Services.TryAddSingleton<WebSocketServer>();

            var mvcBuilder = Services.AddMvcCore(options => {
                var oldModelBinderProviders = options.ModelBinderProviders.ToList();
                var newModelBinderProviders = new IModelBinderProvider[] {
                    new SimpleModelBinderProvider<Moment, MomentModelBinder>(),
                    new SimpleModelBinderProvider<Symbol, SymbolModelBinder>(),
                    new SimpleModelBinderProvider<Session, SessionModelBinder>(),
                    new PageRefModelBinderProvider(),
                };
                options.ModelBinderProviders.Clear();
                options.ModelBinderProviders.AddRange(newModelBinderProviders);
                options.ModelBinderProviders.AddRange(oldModelBinderProviders);
            });

            // Newtonsoft.Json serializer is optional starting from v1.4+
            /*
            mvcBuilder.AddNewtonsoftJson(options => {
                MemberwiseCopier.Invoke(
                    NewtonsoftJsonSerializer.DefaultSettings,
                    options.SerializerSettings,
                    copier => copier with {
                        Filter = member => member.Name != "Binder",
                    });
            });
            */
        }

        public FusionWebServerBuilder AddControllers(
            Action<IServiceProvider, SignInController.Options>? signInControllerOptionsBuilder = null)
        {
            Services.TryAddSingleton(c => {
                var options = new SignInController.Options();
                signInControllerOptionsBuilder?.Invoke(c, options);
                return options;
            });
            Services.AddControllers()
                .AddApplicationPart(typeof(AuthController).Assembly);
            return this;
        }

        public FusionWebServerBuilder AddControllerFilter(Func<TypeInfo, bool> controllerFilter)
        {
            Services.AddControllers()
                .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(
                    new ControllerFilter(controllerFilter)));
            return this;
        }
    }
}

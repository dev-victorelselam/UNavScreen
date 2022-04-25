using Cysharp.Threading.Tasks;
using Elselam.UnityRouter.Domain;
using Elselam.UnityRouter.History;
using Elselam.UnityRouter.Installers;
using Elselam.UnityRouter.Transitions;
using Elselam.UnityRouter.Url;
using Elselam.UnityRouter.Tests.Mocks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;
using Assets.Package.Navigation.Scripts.Loader;
using System;
using Assets.Package.Navigation.Scripts.Loader.SpecificLoaders;

namespace Elselam.UnityRouter.Tests
{
    [TestFixture]
    public class NavigationTests
    {
        private List<ScreenScheme> historyList;
        private IHistory history;
        private IUrlManager urlManager;
        private ICurrentScreen currentScreen;
        private INavigation navigation;

        private ScreenScheme enterSchemeSent = null;
        private ScreenScheme exitSchemeSent = null;
        private DiContainer container;

        [SetUp]
        public void Binding()
        {
            container = new DiContainer(StaticContext.Container);
            string appDomain = "domain://";

            historyList = new List<ScreenScheme>();
            container.Bind<IHistory>()
                .FromMethod(_ =>
                {
                    var history = Substitute.For<IHistory>();
                    history.When(h => h.Add(Arg.Any<ScreenScheme>()))
                        .Do(callInfo => historyList.Add(callInfo.Arg<ScreenScheme>()));
                    history.Add(Arg.Any<ScreenScheme>()).Returns(true);
                    return history;
                }).AsSingle();

            container.Bind<ICurrentScreen>()
                .To<MockCurrentScreen>()
                .AsSingle();

            container.Bind<ISpecificLoader>()
                .FromMethod(_ =>
                {
                    var loader = Substitute.For<ISpecificLoader>();
                    loader.Load(Arg.Any<ScreenScheme>(), Arg.Any<ScreenScheme>(), Arg.Any<ITransition>(), Arg.Any<bool>()).Returns(info =>
                    {
                        if (info.ArgAt<ScreenScheme>(1) == null)
                            return UniTask.Create(() => new UniTask<ScreenScheme>(null));
                        return UniTask.Create(() => new UniTask<ScreenScheme>(new ScreenScheme("", "")));
                    });

                    loader
                    .When(s => s.Load(Arg.Any<ScreenScheme>(), Arg.Any<ScreenScheme>(), Arg.Any<ITransition>(), Arg.Any<bool>()))
                    .Do(args =>
                    {
                        enterSchemeSent = args.ArgAt<ScreenScheme>(0);
                        exitSchemeSent = args.ArgAt<ScreenScheme>(1);
                    });

                    return loader;
                });

            container.Bind<ILoaderFactory>()
                .FromMethod(_ =>
                {
                    var factory = Substitute.For<ILoaderFactory>();
                    factory.GetLoader(Arg.Any<string>(), Arg.Any<string>()).Returns(_ => container.Resolve<ISpecificLoader>());
                    return factory;
                }).AsSingle();

            container.Bind<IScreenResolver>()
                .FromMethod(_ =>
                {
                    var resolver = Substitute.For<IScreenResolver>();
                    var registries = container.Resolve<List<IScreenRegistry>>();
                    resolver.ResolveFirstScreen().Returns(_ => new ScreenScheme("", "MockScreenA"));
                    resolver.GetScreenName(Arg.Any<Type>()).Returns(arg => registries.FirstOrDefault(s => s.ScreenPresenter == arg.Arg<Type>())?.ScreenId);
                    return resolver;
                }).AsSingle();

            container.Bind<IUrlDomainProvider>()
                .FromMethod(_ =>
                {
                    var urlProvider = Substitute.For<IUrlDomainProvider>();
                    urlProvider.Url.Returns(appDomain);
                    return urlProvider;
                });

            container.Bind<IUrlManager>()
                .To<UrlManager>()
                .AsSingle();

            container.Bind<ITransition>()
                .FromMethod(_ =>
                {
                    var transition = Substitute.For<ITransition>();
                    transition.Transite(Arg.Any<IScreenPresenter>(), Arg.Any<IScreenPresenter>())
                        .Returns(info => UniTask.CompletedTask);
                    return transition;
                }).AsSingle();

            container.Bind<IScreenRegistry>()
                .FromMethod(_ =>
                {
                    var defaultScreen = Substitute.For<IScreenRegistry>();
                    defaultScreen.ScreenId.Returns("MockScreenA");
                    defaultScreen.ScreenPresenter.Returns(typeof(ScreenAPresenter));
                    return defaultScreen;
                }).AsSingle();

            container.Bind<INavigation>()
                .To<NavigationManager>()
                .AsSingle();

            ScreenMocks.RegisterScreensModels(container);
            container.Inject(this);
        }

        [Inject]
        public void Construct(
            IHistory history,
            IUrlManager urlManager, 
            ICurrentScreen currentScreen,
            INavigation navigation)
        {
            this.history = history;
            this.urlManager = urlManager;
            this.currentScreen = currentScreen;
            this.navigation = navigation;

            navigation.Initialize();
        }

        [Test]
        public void NavigateTo_WithoutParams_CallLoadScreenWithScreenId()
        {
            navigation.NavigateTo<ScreenAPresenter>();

            enterSchemeSent.ScreenId.Should().Be("MockScreenA");
        }

        [Test]
        public void NavigateTo_WithParams_CallLoadScreenWithScreenIdAndParameters()
        {
            var parameters = new Dictionary<string, string> {
                {"ItemLength", "15"},
                {"ItemPosition", JsonUtility.ToJson(new Vector3(15f, 10f, 5f))},
            };

            navigation.NavigateTo<CustomScreenPresenter>(parameters: parameters);

            enterSchemeSent.Should().NotBeNull();
            enterSchemeSent.ScreenId.Should().Be("MockScreenCustom");
            enterSchemeSent.Parameters["ItemLength"].Should().Be("15");
            enterSchemeSent.Parameters["ItemPosition"].Should().Be(JsonUtility.ToJson(new Vector3(15f, 10f, 5f)));
        }

        [Test]
        public void NavigateTo_ValidScreen_AddOldScreenSchemeToHistory()
        {
            navigation.NavigateTo<SameScreenPresenter>();

            navigation.NavigateTo<SameScreenPresenter>();

            history.Received(1).Add(Arg.Any<ScreenScheme>());
        }

        [Test]
        public void NavigateTo_ValidScreen_SetCurrentScreen()
        {
            navigation.NavigateTo<SameScreenPresenter>();

            navigation.NavigateTo<CustomScreenPresenter>();

            currentScreen.Scheme.Should().Be(enterSchemeSent);
        }

        [Test]
        public void NavigateTo_ValidScene_AddOldScreenSchemeToHistory()
        {
            navigation.NavigateTo<ScreenAPresenter>();

            navigation.NavigateTo("TestScene");

            history.Received(1).Add(Arg.Any<ScreenScheme>());
        }

        [Test]
        public void NavigateTo_InvalidScreen_Failure()
        {
            NavigationException error = null;

            try
            {
                navigation.NavigateTo<UnregisteredScreenPresenter>();
            }
            catch (NavigationException e)
            {
                error = e;
            }

            error.Should().NotBeNull();
        }

        [Test]
        public void NavigateTo_EmptyScreenId_Failure()
        {
            NavigationException error = null;

            try
            {
                navigation.NavigateTo<EmptyIdPresenter>();
            }
            catch (NavigationException e)
            {
                error = e;
            }

            error.Should().NotBeNull();
        }

        [Test]
        public void NavigateTo_UsingValidDeepLink_CallLoadScreenWithIdAndParameters()
        {
            var urlProvider = container.Resolve<IUrlDomainProvider>();
            var url = $"{urlProvider.Url}MockScreenA?test=true";
            var scheme = urlManager.Deserialize(url);

            navigation.NavigateTo(scheme);

            enterSchemeSent.ScreenId.Should().Be("MockScreenA");
            enterSchemeSent.Parameters["test"].Should().Be("true");
        }

        [Test]
        public void BackToLastScreen_WithFilledHistory_CallLoadScreenWithIdAndParameters()
        {
            var parameters = new Dictionary<string, string> {{"test", "true"}};
            currentScreen.SetCurrentScreen(new ScreenScheme("", "MockScreenB"));
            history.Back().Returns(new ScreenScheme("", "MockScreenA", parameters));

            navigation.BackToLastScreen();

            enterSchemeSent.Should().NotBeNull();
            enterSchemeSent.ScreenId.Should().Be("MockScreenA");
            enterSchemeSent.Parameters["test"].Should().Be("true");
        }

        [Test]
        public void BackToLastScreen_DontAddHistory()
        {
            currentScreen.SetCurrentScreen(new ScreenScheme("", "MockScreenB"));
            history.Back().Returns(new ScreenScheme("", "MockScreenA"));

            navigation.BackToLastScreen();

            history.Received(0).Add(Arg.Any<ScreenScheme>());
        }

        [Test]
        public void BackToLastScreen_WithEmptyHistory_Failure()
        {
            NavigationException error = null;
            history.Back().Returns(_ => null);
            var transition = container.Resolve<ITransition>();

            try
            {
                navigation.BackToLastScreen(transition);
            }
            catch (NavigationException e)
            {
                error = e;
            }

            error.Should().NotBeNull();
        }

        [Test]
        public void LoadScene_CallSceneLoaderWithLoadScreen()
        {
            var sceneName = "TestScene";
            navigation.NavigateTo<ScreenAPresenter>();

            navigation.NavigateTo(sceneName);

            enterSchemeSent.ScreenId.Should().Be(sceneName);
        }
    }
}
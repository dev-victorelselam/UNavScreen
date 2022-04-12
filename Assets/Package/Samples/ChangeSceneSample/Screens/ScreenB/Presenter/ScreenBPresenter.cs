using Elselam.UnityRouter.Domain;
using Elselam.UnityRouter.Extensions;
using Elselam.UnityRouter.Installers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Sample.ChangeSceneSample.Screens.ScreenB.Presenter
{
    public class ScreenBPresenter : BaseCanvasScreenPresenter
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Button loadScene;

        private float elementPosition = 0;
        private INavigation navigation;
        private IParameterManager parameterManager;

        [Inject]
        public void Inject(INavigation navigation, IParameterManager parameterManager)
        {
            this.navigation = navigation;
            this.parameterManager = parameterManager;

            loadScene.onClick.AddListener(LoadScene);
        }

        public override void OnEnter(IDictionary<string, string> parameters)
        {
            elementPosition = parameterManager.GetParamOfType<float>(parameters, "elementPosition", defaultValue: elementPosition);
            slider.value = elementPosition;
        }

        public void BackToLastScreen()
        {
            navigation.BackToLastScreen();
        }

        public void LoadScene()
        {
            navigation.NavigateTo("SceneWithVideoPlayer");
        }

        public void UpdateElementPosition(float position)
        {
            elementPosition = position;
        }

        public override IDictionary<string, string> OnExit()
        {
            var paramPosition = parameterManager.Create("elementPosition", slider.value);
            var parameters = parameterManager.CreateDictionary(paramPosition);
            return parameters;
        }
    }
}

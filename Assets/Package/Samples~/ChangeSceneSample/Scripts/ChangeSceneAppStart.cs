using elselam.Navigation.Navigation;
using UnityEngine;
using Zenject;

namespace Sample.ChangeSceneSample.Scripts {
    public class ChangeSceneAppStart : MonoBehaviour {
        private INavigation navigation;

        [Inject]
        public void Construct(INavigation navigation) {
            this.navigation = navigation;
        }

        private void Start() {
            navigation.Initialize();
        }
    }
}
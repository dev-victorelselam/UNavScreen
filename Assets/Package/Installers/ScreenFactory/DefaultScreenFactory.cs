﻿using Elselam.UnityRouter.Domain;
using UnityEngine;

namespace Elselam.UnityRouter.Installers
{
    public class DefaultScreenFactory : IScreenFactory
    {
        private readonly Transform screensContainer;

        public DefaultScreenFactory(Transform screensContainer)
        {
            this.screensContainer = screensContainer;
        }

        public IScreenModel Create(IScreenRegistry screenRegistry)
        {
            var presenter = Object.Instantiate(screenRegistry.ScreenPrefab, screensContainer);
            presenter.Disable();
            return new ScreenModel(screenRegistry.ScreenId, presenter);
        }
    }
}
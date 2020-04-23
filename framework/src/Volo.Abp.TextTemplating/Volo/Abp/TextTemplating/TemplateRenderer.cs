﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Localization;
using Scriban;
using Scriban.Runtime;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace Volo.Abp.TextTemplating
{
    public class TemplateRenderer : ITemplateRenderer, ITransientDependency
    {
        private readonly ITemplateContentProvider _templateContentProvider;
        private readonly ITemplateDefinitionManager _templateDefinitionManager;
        private readonly IStringLocalizerFactory _stringLocalizerFactory;

        public TemplateRenderer(
            ITemplateContentProvider templateContentProvider,
            ITemplateDefinitionManager templateDefinitionManager,
            IStringLocalizerFactory stringLocalizerFactory)
        {
            _templateContentProvider = templateContentProvider;
            _templateDefinitionManager = templateDefinitionManager;
            _stringLocalizerFactory = stringLocalizerFactory;
        }

        public virtual async Task<string> RenderAsync(
            [NotNull] string templateName,
            [CanBeNull] object model = null,
            [CanBeNull] string cultureName = null,
            [CanBeNull] Dictionary<string, object> globalContext = null)
        {
            Check.NotNullOrWhiteSpace(templateName, nameof(templateName));

            cultureName ??= CultureInfo.CurrentUICulture.Name;

            using (CultureHelper.Use(cultureName))
            {
                return await RenderInternalAsync(
                    templateName,
                    globalContext ?? new Dictionary<string, object>(),
                    model
                );
            }
        }

        protected virtual async Task<string> RenderInternalAsync(
            string templateName,
            Dictionary<string, object> globalContext,
            object model = null)
        {
            var templateDefinition = _templateDefinitionManager.Get(templateName);

            var renderedContent = await RenderSingleTemplateAsync(
                templateDefinition,
                globalContext,
                model
            );

            if (templateDefinition.Layout != null)
            {
                globalContext["content"] = renderedContent;
                renderedContent = await RenderInternalAsync(
                    templateDefinition.Layout,
                    globalContext
                );
            }

            return renderedContent;
        }

        protected virtual async Task<string> RenderSingleTemplateAsync(
            TemplateDefinition templateDefinition,
            Dictionary<string, object> globalContext,
            object model = null)
        {
            var rawTemplateContent = await _templateContentProvider
                .GetContentOrNullAsync(
                    templateDefinition
                );

            return await RenderTemplateContentWithScribanAsync(
                templateDefinition,
                rawTemplateContent,
                globalContext,
                model
            );
        }

        protected virtual async Task<string> RenderTemplateContentWithScribanAsync(
            TemplateDefinition templateDefinition,
            string templateContent,
            Dictionary<string, object> globalContext,
            object model = null)
        {
            var context = CreateScribanTemplateContext(
                templateDefinition,
                globalContext,
                model
            );

            return await Template
                    .Parse(templateContent)
                    .RenderAsync(context);
        }

        protected virtual TemplateContext CreateScribanTemplateContext(
            TemplateDefinition templateDefinition,
            Dictionary<string, object> globalContext,
            object model = null)
        {
            var context = new TemplateContext();

            var scriptObject = new ScriptObject();

            scriptObject.Import(globalContext);

            if (model != null)
            {
                scriptObject["model"] = model;
            }

            if (templateDefinition.LocalizationResource != null)
            {
                var localizer = _stringLocalizerFactory.Create(templateDefinition.LocalizationResource);
                scriptObject.Import(
                    "l",
                    new Func<string, string>(
                        name => localizer[name]
                    )
                );
            }

            context.PushGlobal(scriptObject);

            return context;
        }
    }
}
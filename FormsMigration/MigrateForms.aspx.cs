﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI;
using Telerik.Sitefinity.Forms.Model;
using Telerik.Sitefinity.Frontend.ContentBlock.Mvc.Controllers;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Controllers;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Controllers.Base;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields;
using Telerik.Sitefinity.Frontend.Forms.Mvc.Models.Fields.BackendConfigurators;
using Telerik.Sitefinity.Model.Localization;
using Telerik.Sitefinity.Modules.Forms;
using Telerik.Sitefinity.Modules.Forms.Web.UI.Fields;
using Telerik.Sitefinity.Mvc.Proxy;
using Telerik.Sitefinity.Pages.Model;
using Telerik.Sitefinity.Security;
using Telerik.Sitefinity.Security.Model;
using Telerik.Sitefinity.Utilities.TypeConverters;
using Telerik.Sitefinity.Web.UI.Fields;

namespace SitefinityWebApp
{
    public partial class MigrateForms : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            this.MigrateButton.Click += MigrateButton_Click;
        }

        void MigrateButton_Click(object sender, EventArgs e)
        {
            this.Migrate();
        }

        public void Migrate()
        {
            var formsManager = FormsManager.GetManager();
            var formDescriptions = formsManager.GetForms().Where(f => f.Framework != FormFramework.Mvc).ToList();

            foreach (var formDescription in formDescriptions)
            {
                var formName = formDescription.Name + "_MVC";

                var duplicateForm = this.Duplicate(formDescription, formName, formsManager);
                duplicateForm.Framework = FormFramework.Mvc;
                duplicateForm.Title = formDescription.Title + "_MVC";
            }

            formsManager.SaveChanges();
        }

        public IList<FormDescription> GetFormDescriptions()
        {
            var formsManager = FormsManager.GetManager();
            var formDescriptions = formsManager.GetForms().Where(f => f.Framework != FormFramework.Mvc && f.Title == "TestMe").ToList();

            return formDescriptions;
        }

        public virtual FormDescription Duplicate(FormDescription formDescription, string formName, FormsManager manager)
        {
            var duplicateForm = manager.CreateForm(formName);
            var thisFormMaster = manager.Lifecycle.GetMaster(formDescription);

            // Form has been unpublished
            if (thisFormMaster == null)
            {
                this.CopyFormCommonData(formDescription, duplicateForm, formDescription.Id, manager);

                // Get permissions from ParentForm, because FormDraft is no ISecuredObject
                duplicateForm.CopySecurityFrom(formDescription as ISecuredObject, null, null);
            }
            else
            {
                this.CopyFormCommonData(thisFormMaster, duplicateForm, formDescription.Id, manager);

                // Get permissions from ParentForm, because FormDraft is no ISecuredObject
                duplicateForm.CopySecurityFrom(thisFormMaster.ParentForm as ISecuredObject, null, null);
            }

            duplicateForm.Controls
                .ToList()
                .ForEach(c => c.Published = false);

            return duplicateForm;
        }

        /// <summary>
        /// Copies the data from one IFormData object to another. Note: this method only copies common draft and non-draft
        /// data.
        /// </summary>
        /// <param name="formFrom">The source object.</param>
        /// <param name="formTo">The target object.</param>
        public void CopyFormCommonData<TControlA, TControlB>(IFormData<TControlA> formFrom, IFormData<TControlB> formTo, Guid formId, FormsManager manager)
            where TControlA : ControlData
            where TControlB : ControlData
        {
            formTo.LastControlId = formFrom.LastControlId;
            formTo.CssClass = formFrom.CssClass;
            formTo.FormLabelPlacement = formFrom.FormLabelPlacement;
            formTo.RedirectPageUrl = formFrom.RedirectPageUrl;
            formTo.SubmitAction = formFrom.SubmitAction;
            formTo.SubmitRestriction = formFrom.SubmitRestriction;

            formTo.SubmitActionAfterUpdate = formFrom.SubmitActionAfterUpdate;
            formTo.RedirectPageUrlAfterUpdate = formFrom.RedirectPageUrlAfterUpdate;

            LocalizationHelper.CopyLstring(formFrom.SuccessMessage, formTo.SuccessMessage);

            LocalizationHelper.CopyLstring(formFrom.SuccessMessageAfterFormUpdate, formTo.SuccessMessageAfterFormUpdate);

            this.CopyControls(formFrom.Controls, formTo.Controls, manager, formId);

            manager.CopyPresentation(formFrom.Presentation, formTo.Presentation);
        }

        public void CopyControls<SrcT, TrgT>(IEnumerable<SrcT> source, IList<TrgT> target, FormsManager manager, Guid formId)
            where SrcT : ControlData
            where TrgT : ControlData
        {
            var traverser = new FormControlTraverser<SrcT, TrgT>(source, target, this.CopyControl<SrcT, TrgT>, manager);
            traverser.CopyControls("Body", "Body");
        }

        private TrgT CopyControl<SrcT, TrgT>(SrcT currentSourceControl, FormsManager manager)
            where SrcT : ControlData
            where TrgT : ControlData
        {
            TrgT migratedCotnrolData;
            if (!currentSourceControl.IsLayoutControl)
            {
                var migratedControl = this.ConfigureFormControl(currentSourceControl, currentSourceControl.ContainerId, manager);

                // Placeholder is updated later.
                migratedCotnrolData = manager.CreateControl<TrgT>(migratedControl, "Body");
            }
            else
            {
                migratedCotnrolData = manager.CreateControl<TrgT>();
                manager.CopyControl(currentSourceControl, migratedCotnrolData);
            }

            return migratedCotnrolData;
        }

        /// <summary>
        /// Prepares a Form control for display in the backend.
        /// </summary>
        /// <param name="formControl">The form control.</param>
        /// <param name="formId">Id of the form that hosts the field.</param>
        /// <returns>The configured form control.</returns>
        public Control ConfigureFormControl(ControlData formControlData, Guid formId, FormsManager manager)
        {
            Control control = manager.LoadControl(formControlData, null);

            var controlType = control.GetType();

            FieldConfiguration fieldConfiguration = null;
            foreach (var pair in this.fieldMap)
            {
                if (pair.Key.IsAssignableFrom(controlType))
                {
                    fieldConfiguration = pair.Value;
                    if (pair.Value == null)
                        return null;

                    break;
                }
            }

            if (fieldConfiguration == null)
                fieldConfiguration = this.fieldMap[typeof(FormTextBox)];


            var mvcProxy = new MvcControllerProxy();
            mvcProxy.ControllerName = fieldConfiguration.BackendFieldType.FullName;

            var newController = Activator.CreateInstance(fieldConfiguration.BackendFieldType);

            var fieldController = newController as IFormFieldController<IFormFieldModel>;
            var elementController = newController as IFormElementController<IFormElementModel>;
            var formField = control as IFormFieldControl;

            if (fieldController != null && formField != null)
            {
                var fieldControl = formField as FieldControl;
                if (fieldControl != null)
                {
                    fieldController.MetaField = formField.MetaField;
                    fieldController.MetaField.Title = fieldControl.Title;
                    fieldController.Model.ValidatorDefinition = fieldControl.ValidatorDefinition;
                    if (fieldConfiguration.FieldConfigurator != null)
                    {
                        fieldConfiguration.FieldConfigurator.FormId = formId;
                        fieldConfiguration.FieldConfigurator.Configure(fieldControl, fieldController);
                    }
                }

                mvcProxy.Settings = new ControllerSettings((Controller)fieldController);
            }
            else if (elementController != null)
            {
                mvcProxy.Settings = new ControllerSettings((Controller)elementController);
            }

            return (Control)mvcProxy;
        }

        private static readonly Type formFileUploadType = TypeResolutionService.ResolveType("Telerik.Sitefinity.Modules.Forms.Web.UI.Fields.FormFileUpload");
        private readonly Dictionary<Type, FieldConfiguration> fieldMap = new Dictionary<Type, FieldConfiguration>()
            {
                { typeof(FormCheckboxes), new FieldConfiguration(typeof(CheckboxesFieldController), new CheckboxesFieldConfigurator()) },
                { typeof(FormDropDownList), new FieldConfiguration(typeof(DropdownListFieldController), new DropdownFieldConfigurator()) },
                { typeof(FormMultipleChoice), new FieldConfiguration(typeof(MultipleChoiceFieldController), new MultipleChoiceFieldConfigurator()) },
                { typeof(FormParagraphTextBox), new FieldConfiguration(typeof(ParagraphTextFieldController), null) },
                { typeof(FormTextBox), new FieldConfiguration(typeof(TextFieldController), null) },
                { MigrateForms.formFileUploadType, new FieldConfiguration(typeof(FileFieldController), null) },
                { typeof(FormSubmitButton), new FieldConfiguration(typeof(SubmitButtonController), null) },
                { typeof(FormCaptcha),  new FieldConfiguration(typeof(CaptchaController), null) },
                { typeof(FormSectionHeader),  new FieldConfiguration(typeof(SectionHeaderController), null) },
                { typeof(FormInstructionalText),  new FieldConfiguration(typeof(ContentBlockController), null) }
            };

        private class FormControlTraverser<SrcT, TrgT>
            where SrcT : ControlData
            where TrgT : ControlData
        {
            public FormControlTraverser(IEnumerable<SrcT> source, IList<TrgT> target, Func<SrcT, FormsManager, TrgT> copyControlDelegate, FormsManager manager)
            {
                this._source = source;
                this._target = target;
                this._copyControlDelegate = copyControlDelegate;
                this._manager = manager;
            }

            public void CopyControls(string sourcePlaceholder, string targetPlaceholder)
            {
                var controlsToCopy = this._source.Where(c => c.PlaceHolder == sourcePlaceholder).ToDictionary(c => c.SiblingId);

                var currentSourceSiblingId = Guid.Empty;
                var currentTargetSiblingId = Guid.Empty;
                while (controlsToCopy.Count > 0)
                {
                    if (!controlsToCopy.ContainsKey(currentSourceSiblingId))
                        break;

                    var currentSourceControl = controlsToCopy[currentSourceSiblingId];
                    var newControl = this._copyControlDelegate(currentSourceControl, this._manager);
                    newControl.PlaceHolder = targetPlaceholder;
                    newControl.SiblingId = currentTargetSiblingId;

                    this._target.Add(newControl);
                    controlsToCopy.Remove(currentSourceSiblingId);

                    currentSourceSiblingId = currentSourceControl.Id;
                    currentTargetSiblingId = newControl.Id;

                    if (currentSourceControl.PlaceHolders != null && newControl.PlaceHolders != null)
                    {
                        for (var i = 0; i < currentSourceControl.PlaceHolders.Length; i++)
                        {
                            this.CopyControls(currentSourceControl.PlaceHolders[i], newControl.PlaceHolders[Math.Min(i, newControl.PlaceHolders.Length - 1)]);
                        }
                    }
                }
            }

            private readonly IEnumerable<SrcT> _source;
            private readonly IList<TrgT> _target;
            private readonly Func<SrcT, FormsManager, TrgT> _copyControlDelegate;
            private readonly FormsManager _manager;
        }
    }
}
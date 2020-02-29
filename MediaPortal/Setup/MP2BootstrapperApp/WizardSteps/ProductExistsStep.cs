using MP2BootstrapperApp.Commands;
using MP2BootstrapperApp.Models;
using MP2BootstrapperApp.ViewModels;

namespace MP2BootstrapperApp.WizardSteps
{
  public class ProductExistsStep : IStep
  {
    public void Enter(Wizard wizard, InstallWizardViewModel viewModel, Package model, IBootstrapperApplicationModel applicationModel)
    {
      viewModel.Content = new InstallExistTypePageViewModel(model);
      viewModel.NextCommand = new RelayCommand(o => { SetNextStep(wizard, viewModel, model, applicationModel); });
      viewModel.BackCommand = new RelayCommand(o => { SetBackStep(wizard, viewModel, model, applicationModel); });
    }

    private static void SetNextStep(Wizard wizard, InstallWizardViewModel viewModel, Package model, IBootstrapperApplicationModel applicationModel)
    {
      wizard.NextStep = new ProductExistsStep();
      viewModel.Content = new InstallExistTypePageViewModel(model);
      wizard.ChangeStep(viewModel, model, applicationModel);
    }
    
    private static void SetBackStep(Wizard wizard, InstallWizardViewModel viewModel, Package model, IBootstrapperApplicationModel applicationModel)
    {
      wizard.NextStep = new WelcomeStep();
      viewModel.Content = new WelcomePageViewModel(model);
      wizard.ChangeStep(viewModel, model, applicationModel);
    }
  }
}

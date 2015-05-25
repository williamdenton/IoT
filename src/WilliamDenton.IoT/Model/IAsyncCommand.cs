using System.Threading.Tasks;
using System.Windows.Input;

namespace WilliamDenton.IoT.Model
{
    public interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object parameter);
    }
}

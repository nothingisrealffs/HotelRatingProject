using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public interface IAuthenticationService
    {
        bool ValidateConnection(string server, string port, string service, 
                                string username, string password, 
                                out string connectionString, 
                                out DatabaseMode dbMode);
    }
}

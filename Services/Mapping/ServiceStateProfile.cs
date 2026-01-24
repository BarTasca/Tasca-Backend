using AutoMapper;
using BarTasca.DTOs.ServiceState;
using ServiceStateModel = BarTasca.Models.ServiceState;

namespace BarTasca.Services.Mapping
{
    public class ServiceStateMappingProfile : Profile
    {
        public ServiceStateMappingProfile()
        {
            CreateMap<ServiceStateModel, ServiceStateDto>();
        }
    }
}

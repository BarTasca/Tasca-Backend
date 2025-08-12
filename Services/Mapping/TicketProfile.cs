using AutoMapper;
using BarTasca.DTOs.Ticket;
using TicketModel = BarTasca.Models.Ticket;

namespace BarTasca.DTOs.Mapping;

public class TicketMappingProfile : Profile
{
    public TicketMappingProfile()
    {
        CreateMap<TicketModel, TicketDetailDto>()
            .ForMember(d => d.Status, m => m.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.CustomerFullName, m => m.MapFrom(s => s.Customer.FullName))
            .ForMember(d => d.Ahead, m => m.Ignore()); // lo calcula el servicio

        CreateMap<TicketModel, TicketCompactDto>()
            .ForMember(d => d.Status, m => m.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Ahead, m => m.Ignore());
    }
}

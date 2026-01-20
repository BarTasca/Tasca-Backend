using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.ServiceState;
using BarTasca.Models;
using BarTasca.Services.Interfaces;

namespace BarTasca.Services.Services
{
    public class ServiceStateService : IServiceStateService
    {
        private readonly IServiceStateRepository _serviceStateRepository;
        private readonly IMapper _mapper;

        public ServiceStateService(IServiceStateRepository serviceStateRepository, IMapper mapper)
        {
            _serviceStateRepository = serviceStateRepository;
            _mapper = mapper;
        }

        public async Task<ServiceStateDto> GetAsync(CancellationToken ct = default)
        {
            var state = await _serviceStateRepository.GetAsync(ct);
            return _mapper.Map<ServiceStateDto>(state);
        }

        public async Task<bool> SetOpenAsync(UpdateServiceStateDto dto, CancellationToken ct = default)
        {
            return await _serviceStateRepository.SetOpenAsync(dto.IsOpen, ct);
        }
    }
}

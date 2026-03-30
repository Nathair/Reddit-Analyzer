using AutoMapper;
using RedditAnalyzer.Server.Models;

namespace RedditAnalyzer.Server.Mappings
{
    public class RedditMappingProfile : Profile
    {
        public RedditMappingProfile()
        {
            CreateMap<RedditPost, RedditPostDto>();
        }
    }
}

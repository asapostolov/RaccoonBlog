using RaccoonBlog.Web.Infrastructure.AutoMapper.Profiles.Resolvers;
using RaccoonBlog.Web.Infrastructure.Common;

namespace RaccoonBlog.Web.ViewModels
{
	public class PostReference
	{
		// if Id is: posts/1024, than domainId will be: 1024
		public string Id { get; set; }
		public string Title { get; set; }

		private string domainId;
		public string DomainId
		{
			get
			{
				if ( domainId == "0" || domainId == null || domainId == "" ) {
					domainId = Id.CleanPostId();
				}

                return domainId;
			}
		}

		private string slug;
		public string Slug
		{
			get { return slug ?? (slug = SlugConverter.TitleToSlug(Title)); }
			set { slug = value; }
		}
	}
}

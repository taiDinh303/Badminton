using Contract.Repositories.AuthEntity;
using ModelViews.UserInfoModelView;

namespace Services.Mappings
{
    public static class UserInfoMapping
    {
        // Mapping Entity -> UserInfoModelView
        public static UserInfoResponseModelView ToViewModel(this UserInfo? entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var model = new UserInfoResponseModelView
            {
                UserId = entity.Id,
                GivenName = entity.GivenName,
                FamilyName = entity.FamilyName,
                Picture = entity.Picture,
                Gender = entity.Gender,
                Address = entity.Address,
                BirthDate = entity.BirthDate
            };

            return model;
        }
        // Mapping UserInfoModelView -> Entity
        public static UserInfo ToEntity(this UserInfoResponseModelView model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var entity = new UserInfo
            {
                Id = model.UserId,
                GivenName = model.GivenName,
                FamilyName = model.FamilyName,
                Picture = model.Picture,
                Gender = model.Gender,
                Address = model.Address,
                BirthDate = model.BirthDate
            };

            return entity;
        }

        // Mapping UpdateUserInfoModelView -> Entity
        public static void ToEntity(this UpdateUserInfoModelView model, UserInfo entity)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.FamilyName = model.FamilyName == string.Empty ? string.Empty     // allow WhiteSpace
                                : model.FamilyName == null ? entity.FamilyName      // Ignore if field is null
                                : model.FamilyName;

            // Ignore if field is null
            entity.GivenName = !string.IsNullOrWhiteSpace(model.GivenName) ? model.GivenName : entity.GivenName;
            entity.Picture = !string.IsNullOrWhiteSpace(model.Picture) ? model.Picture : entity.Picture;
            entity.Address = !string.IsNullOrWhiteSpace(model.Address) ? model.Address : entity.Address;

            // Với kiểu nullable
            entity.BirthDate = model.BirthDate ?? entity.BirthDate;

            if (model.Gender.HasValue)
                entity.Gender = model.Gender.Value;
        }



    }
}

create procedure cms_AddUser
	@s_UserID varchar(50),
	@s_Pwd varchar(50)
as

	declare @iAffectedRows int;

	set @iAffectedRows = 0
	
	if (not exists (select 1 from dbo.CMS_User where UserID = @s_UserID))
	begin
		insert into dbo.CMS_User (UserID, Pwd)
		select @s_UserID, @s_Pwd;

		set @iAffectedRows = @@rowcount;
	end

select @iAffectedRows AffectedRows;
namespace PrDCOldApp.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddLikesAndComments : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Comments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        User = c.String(),
                        Content = c.String(),
                        ImageEntry_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ImageEntries", t => t.ImageEntry_Id)
                .Index(t => t.ImageEntry_Id);
            
            AddColumn("dbo.ImageEntries", "Likes", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Comments", "ImageEntry_Id", "dbo.ImageEntries");
            DropIndex("dbo.Comments", new[] { "ImageEntry_Id" });
            DropColumn("dbo.ImageEntries", "Likes");
            DropTable("dbo.Comments");
        }
    }
}

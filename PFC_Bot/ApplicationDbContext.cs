using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFC_Bot
{

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            /*modelBuilder.Entity<FightEntity>()
                .Property<ulong>("IdAttacker")
                .HasColumnName("id_attacker");

            modelBuilder.Entity<FightEntity>()
                .Property<ulong>("IdDefender")
                .HasColumnName("id_defender");

            modelBuilder.Entity<FightEntity>()
                .Property<ulong?>("IdWinner")
                .HasColumnName("id_winner");*/


            modelBuilder.Entity<FightEntity>()
                .HasOne<UserEntity>(f => f.Attacker)
                .WithMany(u => u.FightsAttack);

            modelBuilder.Entity<FightEntity>()
                .HasOne<UserEntity>(f => f.Defender)
                .WithMany(u => u.FightsDefend);

            modelBuilder.Entity<FightEntity>()
                .HasOne<UserEntity>(f => f.Winner)
                .WithMany(u => u.FightsWon);
        }
    

        public DbSet<UserEntity> Users { get; set; } = null!;
        public DbSet<FightEntity> Fights { get; set; } = null!;
    }


    [Table("users")]
    public class UserEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        [Column("id")]
        public ulong Id { get; set; }
        [Column("id_discord")]
        public ulong Id_Discord { get; set; }
        [Column("pseudo")]
        public string Pseudo { get; set; }
        [Column("score")]
        public ulong Score { get; set; }
        [Column("money")]
        public int Money { get; set; }
        [Column("signature_sentence")]
        public string Signature_Sentence { get; set; }
        [Column("signature_url")]
        public string Signature_Url { get; set; }


        [Column("notification")]
        public bool Notification { get; set; }
        [Column("is_frozen")]
        public bool Freeze { get; set; }

        public List<FightEntity> FightsAttack { get; }
        public List<FightEntity> FightsDefend { get; }
        public List<FightEntity> FightsWon { get; }
    }


    

    [Table("fights")]
    public class FightEntity
    {

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        [Column("id_fight")]
        public ulong Id_Fight { get; set; }

        [Column("id_attacker")]
        public ulong Id_Attacker { get; set; }

        [Column("id_defender")]
        public ulong Id_Defender { get; set; }

        [Column("id_winner")]
        public ulong? Id_Winner { get; set; }


        [ForeignKey("Id_Attacker")]
        public UserEntity Attacker { get; set; }
        [ForeignKey("Id_Defender")]
        public UserEntity Defender { get; set; }
        [ForeignKey("Id_Winner")]
        public UserEntity Winner { get; set; }

        [Column("choice_attacker")]
        public char? choice_attacker { get; set; }
        [Column("choice_defender")]
        public char? choice_defender { get; set; }

        [Column("id_message_attacker")]
        public ulong? Id_Message_Attacker { get; set; }
        [Column("id_message_defender")]
        public ulong? Id_Message_Defender { get; set; }


        [Column("jump_url_attacker")]
        public string Jump_Url_Attacker { get; set; }

        [Column("jump_url_defender")]
        public string Jump_Url_Defender { get; set; }

        [Column("canceled")]
        public Boolean Canceled { get; set; }

        [Column("posting_date", TypeName = "Date")]
        public DateTime Posting_Date { get; set; }

        [Column("ending_date", TypeName = "Date")]
        public DateTime Ending_Date { get; set; }



        [Column("provock_gif")]
        public string? Provock_Gif { get; set; }
        [Column("provock_sentence")]
        public string? Provock_Sentence { get; set; }
    }
}
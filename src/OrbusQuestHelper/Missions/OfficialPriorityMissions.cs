using System;

namespace OrbusQuestHelper;

/// <summary>
/// Compact original labels and tier increments derived from eight public
/// Community Reborn mission definition files. Description is identified
/// by a deterministic FNV-1a 64 fingerprint of its UTF-16 characters,
/// NOT by copying story paragraphs into the DLL. Level missions also
/// match their real ID + type. These facts are fallback DISPLAY metadata:
/// never mutate native mission progress or override live reward tiers.
/// </summary>
internal static class OfficialPriorityMissions
{
    internal sealed record Definition(string Id, string Type,
        ulong DescriptionHash, string Action, double Bronze,
        double Silver, double Gold, string Unit);

    private static readonly Definition[] Entries =
    {
        new("", "kills", 0xa9aed6ef6a328805UL,
            "Defeat Stafrushers", 7, 3, 5, " defeated"),
        new("", "kills", 0x9337bcc0422e448bUL,
            "Defeat Barking Stafrushers", 5, 5, 5, " defeated"),
        new("", "kills", 0x13af8215be19007fUL,
            "Defeat Stafrasters", 4, 2, 4, " defeated"),
        new("", "kills", 0x0f81c89ded613ad6UL,
            "Defeat Stafrushers, Stafrages & Volur Stafrage", 5, 6, 4, " defeated"),
        new("", "kills", 0x98a217f0c0b94dacUL,
            "Defeat Rushed Stafrushers", 10, 7, 7, " defeated"),
        new("", "kills", 0xd01f1f58da88a32fUL,
            "Defeat any monsters", 10, 8, 7, " defeated"),
        new("", "kills", 0x5c3785301b707c88UL,
            "Defeat Stafrutes", 7, 3, 5, " defeated"),
        new("", "kills", 0xeb4dd07d277abdf2UL,
            "Defeat Undead, Dungeon Troll & Lich King", 4, 8, 10, " defeated"),
        new("", "kills", 0xf64b6825b70cf891UL,
            "Defeat Sewer Rats, Mutated Rats & Slime", 4, 8, 10, " defeated"),
        new("", "kills", 0x4096ad46a213ca59UL,
            "Defeat Fortified Stafrutes", 4, 3, 5, " defeated"),
        new("", "kills", 0xc9b7977242bd9cefUL,
            "Defeat the Horde", 14, 6, 10, " defeated"),
        new("", "kills", 0xab4668098b9e71efUL,
            "Defeat Stafrusher, Stafraster & Sorceress", 10, 6, 12, " defeated"),
        new("", "kills", 0x98fd98da425e7995UL,
            "Defeat Colossus", 1, 1, 2, " defeated"),
        new("", "kills", 0x1aba34c0a5b5788dUL,
            "Defeat Tear Casters", 4, 6, 5, " defeated"),
        new("", "kills", 0xb12bcd6def4333ceUL,
            "Defeat Tear Brutes", 7, 3, 5, " defeated"),
        new("", "kills", 0xbb02c8659529de55UL,
            "Defeat Tear Wizards", 3, 3, 4, " defeated"),
        new("", "kills", 0xf60ebd52f2549ee0UL,
            "Defeat General Minion, Void Caster & Tear General", 7, 5, 4, " defeated"),
        new("", "kills", 0xecf7c253e7e1d709UL,
            "Defeat Dungeon enemies, Minotaur & Gorgon", 12, 10, 8, " defeated"),
        new("", "kills", 0x1b5197610c26cf0bUL,
            "Defeat Tear Wizard, Chaos Purity & Chaos Hunter", 12, 10, 8, " defeated"),
        new("", "kills", 0x9e3a251c0194449dUL,
            "Defeat Scavs", 10, 5, 8, " defeated"),
        new("", "kills", 0xa997580b9b89e47cUL,
            "Defeat Pistoler Scavs", 7, 6, 7, " defeated"),
        new("", "kills", 0x162ddecf54c64d78UL,
            "Defeat Bucket Scav, Sniper Scav & Tohmain", 7, 5, 8, " defeated"),
        new("", "kills", 0xe80795bcf6d210c1UL,
            "Defeat Elite Tear Minions", 7, 6, 7, " defeated"),
        new("", "kills", 0x1c77bc5a462cba4dUL,
            "Defeat Elite Tear Wizards", 7, 3, 7, " defeated"),
        new("", "kills", 0xb0e563c810287bddUL,
            "Defeat Elite Stafrusher, Stafraster & Stafrage", 5, 5, 10, " defeated"),
        new("", "kills", 0x53b723a42fb7f0eeUL,
            "Defeat Highsteppe Giant", 1, 1, 1, " defeated"),
        new("", "kills", 0x99b383bf420fba4dUL,
            "Defeat Chaos Giant", 1, 1, 1, " defeated"),
        new("", "kills", 0x52a667e70322c810UL,
            "Defeat Giant Scav", 1, 1, 1, " defeated"),
        new("hunter1", "kills", 0x1a57769c3c21f189UL,
            "Defeat Sapling Stafrushers", 3, 3, 6, " defeated"),
        new("hunter2", "kills", 0x79f2c8391f456c3cUL,
            "Defeat Sapling Barkers", 3, 2, 4, " defeated"),
        new("hunter3", "kills", 0xa9aed6ef6a328805UL,
            "Defeat Stafrushers", 7, 3, 5, " defeated"),
        new("hunter4", "kills", 0x9337bcc0422e448bUL,
            "Defeat Barking Stafrushers", 5, 5, 5, " defeated"),
        new("hunter5", "kills", 0x13af8215be19007fUL,
            "Defeat Stafrasters", 4, 2, 4, " defeated"),
        new("hunter6", "kills", 0x7e6b4cf7b1fc9194UL,
            "Defeat Stafrushers, Stafrages & Volur Stafrage", 5, 6, 4, " defeated"),
        new("hunter7", "kills", 0x98a217f0c0b94dacUL,
            "Defeat Rushed Stafrushers", 10, 7, 7, " defeated"),
        new("hunter8", "kills", 0xd01f1f58da88a32fUL,
            "Defeat any monsters", 10, 8, 7, " defeated"),
        new("hunter9", "kills", 0x5c3785301b707c88UL,
            "Defeat Stafrutes", 7, 3, 5, " defeated"),
        new("hunter10", "kills", 0xeb4dd07d277abdf2UL,
            "Defeat Undead, Dungeon Troll & Lich King", 4, 8, 10, " defeated"),
        new("hunter11", "kills", 0xf64b6825b70cf891UL,
            "Defeat Sewer Rats, Mutated Rats & Slime", 4, 8, 10, " defeated"),
        new("hunter12", "kills", 0x4096ad46a213ca59UL,
            "Defeat Fortified Stafrutes", 4, 3, 5, " defeated"),
        new("hunter13", "kills", 0xc9b7977242bd9cefUL,
            "Defeat the Horde", 14, 6, 10, " defeated"),
        new("hunter14", "kills", 0xab4668098b9e71efUL,
            "Defeat Stafrusher, Stafraster & Sorceress", 10, 6, 12, " defeated"),
        new("hunter15", "kills", 0x98fd98da425e7995UL,
            "Defeat Colossus", 1, 1, 2, " defeated"),
        new("hunter16", "kills", 0x1aba34c0a5b5788dUL,
            "Defeat Tear Casters", 4, 6, 5, " defeated"),
        new("hunter17", "kills", 0xb12bcd6def4333ceUL,
            "Defeat Tear Brutes", 7, 3, 5, " defeated"),
        new("hunter18", "kills", 0xbb02c8659529de55UL,
            "Defeat Tear Wizards", 3, 3, 4, " defeated"),
        new("hunter19", "kills", 0xf60ebd52f2549ee0UL,
            "Defeat General Minion, Void Caster & Tear General", 7, 5, 4, " defeated"),
        new("hunter20", "kills", 0xecf7c253e7e1d709UL,
            "Defeat Dungeon enemies, Minotaur & Gorgon", 12, 10, 8, " defeated"),
        new("hunter21", "kills", 0x1b5197610c26cf0bUL,
            "Defeat Tear Wizard, Chaos Purity & Chaos Hunter", 12, 10, 8, " defeated"),
        new("hunter22", "kills", 0x9e3a251c0194449dUL,
            "Defeat Scavs", 10, 5, 8, " defeated"),
        new("hunter23", "kills", 0xa997580b9b89e47cUL,
            "Defeat Pistoler Scavs", 7, 6, 7, " defeated"),
        new("hunter24", "kills", 0x162ddecf54c64d78UL,
            "Defeat Bucket Scav, Sniper Scav & Tohmain", 7, 5, 8, " defeated"),
        new("hunter25", "kills", 0xe80795bcf6d210c1UL,
            "Defeat Elite Tear Minions", 7, 6, 7, " defeated"),
        new("hunter26", "kills", 0x1c77bc5a462cba4dUL,
            "Defeat Elite Tear Wizards", 7, 3, 7, " defeated"),
        new("hunter27", "kills", 0xb0e563c810287bddUL,
            "Defeat Elite Stafrusher, Stafraster & Stafrage", 5, 5, 10, " defeated"),
        new("hunter28", "kills", 0x53b723a42fb7f0eeUL,
            "Defeat Highsteppe Giant", 1, 1, 1, " defeated"),
        new("hunter29", "kills", 0x99b383bf420fba4dUL,
            "Defeat Chaos Giant", 1, 1, 1, " defeated"),
        new("hunter30", "kills", 0x52a667e70322c810UL,
            "Defeat Giant Scav", 1, 1, 1, " defeated"),
        new("", "fishcaughtweight", 0xa8df3a372581a98cUL,
            "Catch fish by weight near Highsteppe", 35, 20, 30, " weight caught"),
        new("", "fishcaughtweight", 0x41fd208619f0ac3dUL,
            "Catch fish by weight in Rainforest", 35, 20, 30, " weight caught"),
        new("", "fishcaughtweight", 0xd8c0ece58684e5a4UL,
            "Catch fish by weight in Hulthine's Basin", 35, 20, 30, " weight caught"),
        new("", "fishcaughtweight", 0xdd843c1ff91c8d8bUL,
            "Catch fish by weight in Wastelands", 35, 20, 30, " weight caught"),
        new("", "fishcaught", 0xb96059b22f87ccafUL,
            "Catch Sunfish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x781ac54bce0d2e83UL,
            "Catch Minnows", 12, 5, 8, " caught"),
        new("", "fishcaught", 0xa29d89c04e963254UL,
            "Catch Green Bellied Bass", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x41cc1a26faf66f02UL,
            "Catch Katyharan Catfish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x6dcbaa63489c5d66UL,
            "Catch Saddled Lion Eel", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x94be8ec9769ffd68UL,
            "Catch Dwarf Shark", 12, 5, 8, " caught"),
        new("", "fishcaught", 0xfc603d8a9ab3974fUL,
            "Catch Axolotol Fish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x495832105367aa79UL,
            "Catch Blimp Fish", 2, 1, 2, " caught"),
        new("", "fishcaught", 0xe5727a3ea30029f9UL,
            "Catch Cloak Fish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x333351d97482543cUL,
            "Catch Clown Fish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0xb18a7bdcbadc086aUL,
            "Catch Phoenix Fish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x99dbcda403e1e806UL,
            "Catch Frosted Perch", 2, 1, 2, " caught"),
        new("", "fishcaught", 0xfc113052ca8e60c5UL,
            "Catch Ridgeback Fish", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x3314a9bf059014e2UL,
            "Catch Spiralback Snail", 12, 5, 8, " caught"),
        new("", "fishcaught", 0x92ff38a8d495d12aUL,
            "Catch Da Magestic Flounder", 2, 1, 2, " caught"),
        new("", "fishcaught", 0x85ba091c93a5e183UL,
            "Catch Jelmiry Fish", 2, 1, 2, " caught"),
        new("", "fishcaught", 0x953aa47be94de016UL,
            "Catch Kylakin Fish", 2, 1, 2, " caught"),
        new("", "fishcaught", 0x3eec5656d3e77e7eUL,
            "Catch any fish near Highsteppe", 15, 5, 10, " caught"),
        new("", "fishcaught", 0x7c8ef7f75dc2ea7eUL,
            "Catch any fish in Rainforest", 15, 5, 10, " caught"),
        new("", "fishcaught", 0xc711fa8eb8406a29UL,
            "Catch any fish in Hulthine's Basin", 15, 5, 10, " caught"),
        new("", "fishcaught", 0x9066b8fdb68a9522UL,
            "Catch any fish in Wastelands", 15, 5, 10, " caught"),
        new("", "fishcaught", 0x18ccc95f953e6c99UL,
            "Catch any fish anywhere", 25, 6, 10, " caught"),
        new("", "fishcaughtweight", 0x1c6f287316aa9e78UL,
            "Catch fish by weight anywhere", 100, 99, 120, " weight caught"),
        new("fishing6", "fishcaughtweight", 0xcfb09c549098263fUL,
            "Catch fish by weight near Kingsport", 15, 10, 20, " weight caught"),
        new("fishing9", "fishcaught", 0xde6fd016662445b1UL,
            "Catch Sunfish", 9, 5, 7, " caught"),
        new("fishing11", "fishcaught", 0x8e5e4719b81de3b3UL,
            "Catch Saddled Lion Eel", 2, 3, 4, " caught"),
        new("fishing13", "fishcaughtweight", 0x426d1fde9039045aUL,
            "Catch fish by weight anywhere", 40, 15, 20, " weight caught"),
        new("fishing15", "fishcaught", 0xde344d0f8fc4e7c9UL,
            "Catch Green Bellied Bass or Catfish", 10, 6, 10, " caught"),
        new("fishing18", "fishcaught", 0xb7810a0962992cf2UL,
            "Catch Clown Fish in Rainforest", 3, 4, 5, " caught"),
        new("fishing20", "fishcaughtweight", 0x0ed3b86780de82e8UL,
            "Catch fish by weight in Hulthine's Basin", 50, 20, 25, " weight caught"),
        new("fishing22", "fishcaught", 0x76edc3374f51a364UL,
            "Catch Minnows", 6, 6, 8, " caught"),
        new("fishing24", "fishcaught", 0x3dc6a73616dec913UL,
            "Catch Axolotol Fish", 3, 4, 4, " caught"),
        new("fishing25", "fishcaughtweight", 0xa629bb277bb745d9UL,
            "Catch fish by weight near Highsteppe", 50, 20, 25, " weight caught"),
        new("fishing26", "fishcaughtweight", 0xff5d42613da1f44fUL,
            "Catch fish by weight anywhere", 65, 25, 50, " weight caught"),
        new("fishing27", "fishcaught", 0x5f9c68353b00975dUL,
            "Catch Ridgeback Fish", 3, 3, 3, " caught"),
        new("fishing28", "fishcaughtweight", 0x53988472fd67509fUL,
            "Catch fish by weight anywhere", 80, 60, 60, " weight caught"),
        new("fishing29", "fishcaughtweight", 0xc07d38bd8434222fUL,
            "Catch fish by weight anywhere", 150, 60, 80, " weight caught"),
        new("", "harvest", 0xd1a0d297a6ffdc60UL,
            "Gather herbs or ore in Rainforest", 10, 8, 10, " gathered"),
        new("", "harvest", 0x61974ac5895b839dUL,
            "Gather herbs or ore near Highsteppe", 10, 8, 10, " gathered"),
        new("", "harvest", 0x89904ae2b54b435eUL,
            "Gather herbs or ore in Lamavora", 10, 8, 10, " gathered"),
        new("", "harvest", 0x170bcb8e3f066498UL,
            "Gather herbs or ore in Wastelands", 10, 8, 10, " gathered"),
        new("", "harvest", 0x5c4709ebacb9bb7aUL,
            "Gather herbs or ore in Hulthine's Basin", 10, 8, 10, " gathered"),
        new("", "brewpotion", 0x1d5c670d00b31ccaUL,
            "Brew Enhanced Healing Potion", 1, 2, 4, " brewed"),
        new("", "brewpotion", 0x75de25d71d27f6a8UL,
            "Brew Enhanced Intellect Potion", 1, 2, 4, " brewed"),
        new("", "brewpotion", 0x84ae97ef710ea3b3UL,
            "Brew Enhanced Strength Potion", 1, 2, 4, " brewed"),
        new("", "brewpotion", 0xce70cad898eee04cUL,
            "Brew Enhanced Vitality Potion", 1, 2, 4, " brewed"),
        new("", "brewpotion", 0x8faf1ff09451e79fUL,
            "Brew Enhanced Strikes Potion", 1, 2, 4, " brewed"),
        new("", "brewpotion", 0xac76f8919c27ad26UL,
            "Brew Drink of Ithecac", 3, 2, 4, " brewed"),
        new("", "brewpotion", 0x4071fcfdaff12f40UL,
            "Brew Fish Scent Attractor", 3, 2, 4, " brewed"),
        new("", "earndram", 0xd2de88b47dae10acUL,
            "Earn Dram", 1000, 1000, 1000, " Dram earned"),
        new("", "earndram", 0xd2de88b47dae10acUL,
            "Earn Dram", 3000, 1500, 2000, " Dram earned"),
        new("jester8", "harvest", 0xa02f355aeebf5c6dUL,
            "Gather herbs near Highsteppe", 8, 6, 12, " gathered"),
        new("jester9", "equipmentsalvage", 0x184016e00a09fdc4UL,
            "Salvage equipment", 1, 2, 3, " salvaged"),
        new("jester11", "earndram", 0x396138b5a1aad4fbUL,
            "Earn Dram", 200, 200, 350, " Dram earned"),
        new("jester13", "harvest", 0xf7c9d11c4f31a8c1UL,
            "Gather Reedflute and Blade Cactus", 6, 8, 6, " gathered"),
        new("jester14", "earndram", 0xd290c20970ae8226UL,
            "Earn Dram", 800, 500, 1000, " Dram earned"),
        new("jester16", "consume", 0xcc7a86e86cc96a8fUL,
            "Drink Healing Potions in Crypt Dungeon", 2, 2, 4, " consumed"),
        new("jester17", "harvest", 0x4b9875f0ef24b98cUL,
            "Gather Blade Cactus", 8, 7, 8, " gathered"),
        new("jester19", "harvest", 0xa2634ed002122258UL,
            "Gather Linstanium", 2, 2, 3, " gathered"),
        new("jester20", "harvest", 0x2421ce7e15abb36fUL,
            "Gather herbs in Lamavora", 10, 5, 6, " gathered"),
        new("jester21", "harvest", 0x1300acaac8a5afa5UL,
            "Gather herbs in Hulthine's Basin", 6, 4, 6, " gathered"),
        new("jester23", "earndram", 0x9c2cd5a7aa2ae2b3UL,
            "Earn Dram", 1200, 500, 1000, " Dram earned"),
        new("jester24", "harvest", 0xf1431832dfa0fc9dUL,
            "Gather herbs in Wastelands", 8, 7, 8, " gathered"),
        new("jester25", "consume", 0xb9a0da543b6a87e8UL,
            "Drink Ithecac Potion in Lamavora", 1, 1, 1, " consumed"),
        new("jester26", "earndram", 0x8695c5d041faf612UL,
            "Earn Dram", 2000, 700, 1300, " Dram earned"),
        new("jester27", "brewpotion", 0x166c90ee61274f37UL,
            "Brew Runesight Potion", 1, 1, 1, " brewed"),
        new("jester28", "harvest", 0x4d422191f80bf167UL,
            "Gather herbs in Wastelands", 8, 7, 8, " gathered"),
        new("jester29", "consume", 0xddaf50cb6e69a949UL,
            "Drink Ruined Potions in Highsteppe", 3, 5, 5, " consumed"),
        new("", "eventparticipation", 0x5833a3fe0978de0bUL,
            "Complete Highsteppe Gate defense", 1, 1, 1, " events completed"),
        new("", "eventparticipation", 0xc9495ddf031acfe8UL,
            "Complete Highsteppe Portal defense", 1, 1, 2, " events completed"),
        new("", "eventgold", 0x10d879fd3a8dba1eUL,
            "Complete Gold-standard Highsteppe events", 1, 1, 1, " Gold events completed"),
        new("", "eventparticipation", 0x7ac3d8583e924159UL,
            "Complete wagon graveyard defense", 3, 1, 3, " events completed"),
        new("", "eventparticipation", 0xf1fc6f835d07c402UL,
            "Complete fish-collection event", 3, 1, 3, " events completed"),
        new("", "eventparticipation", 0x86db335bec2ae4daUL,
            "Complete Bedouin camp defense", 3, 1, 3, " events completed"),
        new("", "eventparticipation", 0xe23597ec82d532e5UL,
            "Complete Highsteppe Gate defense", 3, 1, 3, " events completed"),
        new("", "eventparticipation", 0x1cd03eb45d111008UL,
            "Complete Lamavora ruins defense", 3, 2, 3, " events completed"),
        new("", "eventgold", 0x2e0cfeb7472548faUL,
            "Complete Gold-standard Lamavora ruins event", 2, 1, 2, " Gold events completed"),
        new("", "eventparticipation", 0xfb877c41f409c7c4UL,
            "Complete Knights' fort defense", 4, 2, 4, " events completed"),
        new("", "eventgold", 0x8dabec69242b231bUL,
            "Complete Gold-standard Knights' fort event", 2, 1, 2, " Gold events completed"),
        new("", "eventparticipation", 0xa09ac805b5982ae4UL,
            "Complete Lamavora crystal event", 4, 2, 3, " events completed"),
        new("", "eventgold", 0x45f1c209aab58905UL,
            "Complete Gold-standard crystal event", 2, 1, 2, " Gold events completed"),
        new("", "eventparticipation", 0x15bf46c078a749dfUL,
            "Complete events near Highsteppe", 3, 2, 3, " events completed"),
        new("", "eventparticipation", 0x2dad980c08cb12c3UL,
            "Complete events in Rainforest", 3, 2, 3, " events completed"),
        new("", "eventparticipation", 0xd960e61004952bb3UL,
            "Complete Hulthine ruins defense", 2, 1, 3, " events completed"),
        new("", "eventparticipation", 0xdc2695925269538dUL,
            "Complete any public events", 2, 1, 3, " events completed"),
        new("", "eventparticipation", 0xebfb01082c700af4UL,
            "Complete Wastelands outpost defense", 2, 1, 3, " events completed"),
        new("", "eventparticipation", 0x20f6eb002552028dUL,
            "Complete events in Lamavora", 2, 1, 3, " events completed"),
        new("", "eventgold", 0x4e84f82dc75d2723UL,
            "Complete Gold-standard public events", 2, 1, 3, " Gold events completed"),
        new("event7", "eventparticipation", 0x7571e05bdaecda89UL,
            "Complete Highsteppe Gate defense", 1, 1, 1, " events completed"),
        new("event9", "eventparticipation", 0x75da378619836361UL,
            "Complete Highsteppe Portal defense", 1, 1, 2, " events completed"),
        new("event11", "eventgold", 0x7e2303a8ec68edd0UL,
            "Complete Gold-standard Highsteppe events", 1, 1, 1, " Gold events completed"),
        new("event12", "eventparticipation", 0xc000df83a113ce9bUL,
            "Complete wagon graveyard defense", 3, 1, 3, " events completed"),
        new("event14", "eventparticipation", 0xf1fc6f835d07c402UL,
            "Complete fish-collection event", 3, 1, 3, " events completed"),
        new("event15", "eventparticipation", 0x9384f7c5d8e806efUL,
            "Complete Bedouin camp defense", 3, 1, 3, " events completed"),
        new("event16", "eventparticipation", 0x52e4004d57f46999UL,
            "Complete Highsteppe Gate defense", 3, 1, 3, " events completed"),
        new("event17", "eventparticipation", 0xcbab8bb8fdfb7ff4UL,
            "Complete Lamavora ruins defense", 3, 2, 3, " events completed"),
        new("event18", "eventgold", 0xf9b83ffc92c20e9fUL,
            "Complete Gold-standard Lamavora ruins event", 2, 1, 2, " Gold events completed"),
        new("event19", "eventparticipation", 0xff7cff62deb12bd2UL,
            "Complete Knights' fort defense", 4, 2, 4, " events completed"),
        new("event20", "eventgold", 0x42a6ec9b73d1f56aUL,
            "Complete Gold-standard Knights' fort event", 2, 1, 2, " Gold events completed"),
        new("event21", "eventparticipation", 0x1362ffba30d7a1adUL,
            "Complete Lamavora crystal event", 4, 2, 3, " events completed"),
        new("event22", "eventgold", 0xa9c1054a8320e424UL,
            "Complete Gold-standard crystal event", 2, 1, 2, " Gold events completed"),
        new("event23", "eventparticipation", 0xe81db74ad41d2cd9UL,
            "Complete events near Highsteppe", 3, 2, 3, " events completed"),
        new("event24", "eventparticipation", 0xb22bc42cdf69e41dUL,
            "Complete events in Rainforest", 3, 2, 3, " events completed"),
        new("event25", "eventparticipation", 0xaba29076c96966d2UL,
            "Complete Hulthine ruins defense", 2, 1, 3, " events completed"),
        new("event26", "eventparticipation", 0x79de777febac9653UL,
            "Complete any public events", 2, 1, 3, " events completed"),
        new("event27", "eventparticipation", 0x9a169f8cbe723ecdUL,
            "Complete Wastelands outpost defense", 2, 1, 3, " events completed"),
        new("event28", "eventparticipation", 0x598fd337157b993fUL,
            "Complete events in Lamavora", 2, 1, 3, " events completed"),
        new("event29", "eventgold", 0x87d3dfa2c77851a6UL,
            "Complete Gold-standard public events", 2, 1, 3, " Gold events completed"),
    };

    internal static int Count => Entries.Length;

    internal static ulong HashDescription(string? description)
    {
        ulong hash = 14695981039346656037UL;
        string text = (description ?? "").Trim();
        unchecked
        {
            foreach (char c in text)
            {
                hash ^= (ushort)c;
                hash *= 1099511628211UL;
            }
        }
        return hash;
    }

    internal static bool TryFind(string? missionId, string? missionType,
        string? description, out Definition definition)
    {
        ulong hash = string.IsNullOrWhiteSpace(description) ? 0UL :
            HashDescription(description);
        return TryFindByFingerprint(missionId, missionType, hash,
            out definition);
    }

    // Exposed internally for offline tests using source-derived fingerprints,
    // not network lookups. ID-based quests have an unambiguous source ID.
    internal static bool TryFindByFingerprint(string? missionId,
        string? missionType, ulong hash, out Definition definition)
    {
        definition = null!;
        if (string.IsNullOrWhiteSpace(missionType)) return false;
        string type = missionType.Trim();
        string id = (missionId ?? "").Trim();

        // Real level mission IDs are stable in the server definition.
        // Prefer a matching ID and type to avoid a repeatable quest with
        // the same story text hiding its level-quest requirements.
        if (id.Length > 0)
        {
            foreach (Definition candidate in Entries)
            {
                if (candidate.Id.Length > 0 &&
                    string.Equals(candidate.Id, id,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.Type, type,
                        StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        // Weekly/repeatable mission arrays often have no ID. Description
        // fingerprints identify most, but two Dram missions have exactly
        // the same story with DIFFERENT tier requirements. Never choose
        // an arbitrary target in that situation.
        if (hash == 0UL) return false;
        Definition? found = null;
        foreach (Definition candidate in Entries)
        {
            if (candidate.DescriptionHash != hash ||
                !string.Equals(candidate.Type, type,
                    StringComparison.OrdinalIgnoreCase))
                continue;
            if (found != null &&
                (!string.Equals(found.Action, candidate.Action,
                    StringComparison.Ordinal) ||
                 found.Bronze != candidate.Bronze ||
                 found.Silver != candidate.Silver ||
                 found.Gold != candidate.Gold))
                return false;
            found = candidate;
        }

        if (found == null) return false;
        definition = found;
        return true;
    }
}

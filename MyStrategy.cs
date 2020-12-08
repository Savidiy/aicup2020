using Aicup2020.Model;
using System.Collections.Generic;

namespace Aicup2020
{
    public class MyStrategy
    {
        Dictionary<int, EntityMemory> entityMemories = new Dictionary<int, EntityMemory>();

        #region Служебные переменные
        EntityType[] entityTypesArray = { EntityType.BuilderUnit, EntityType.RangedUnit, EntityType.MeleeUnit, EntityType.Turret, EntityType.House, EntityType.BuilderBase, EntityType.MeleeBase, EntityType.RangedBase, EntityType.Wall, EntityType.Resource };

        bool needPrepare = true;
        #endregion

        int[] maxTargetCountTiers = { 5, 15, 40, 80};
        float[] builderTargetCountTiers = { 1f, 0.75f, 0.7f, 0.6f , 0.3f};
        float[] rangerTargetCountTiers = { 0f, 0.25f, 0.3f, 0.4f, 0.7f };
        float housePopulationDelay = 5f;

        #region клетки где можно построить юнита вокруг здания
        int largeBuildingSize = 5;
        int[] buildingPositionDX = {-1, -1, -1, -1, -1, 0, 1, 2, 3 ,4, 5, 5, 5, 5, 5,  4,  3,  2,  1,  0 };
        int[] buildingPositionDY = { 0,  1,  2,  3,  4, 5, 5, 5, 5, 5, 4, 3, 2, 1, 0, -1, -1, -1, -1, -1 };
        int[] buildingPositionIter = { 0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6, -6, 7, -7, 8, -8, 9, -9, 10 };
        // позиции расположены вот так
        //   5  6  7  8  9  
        // 4                10
        // 3                11
        // 2                12
        // 1                13
        // 0  X             14
        //   19 18 17 16 15
        #endregion

        Dictionary<EntityType, Group> basicEntityIdGroups = new Dictionary<EntityType, Group>();
        Group houseBuilderGroup = new Group();
        Group repairBuilderGroup = new Group();
        List<int> needRepairEntityIdList = new List<int>();
        bool hasInactiveHouse = false;

        PlayerView _playerView;
        public IDictionary<EntityType, EntityProperties> properties;

        System.Random random = new System.Random();

        int[][] cellWithIdOnlyBuilding;
        int[][] cellWithIdAny;
        int[][] onceVisibleMap;

        #region Статичстические переменные
        Dictionary<Model.EntityType, int> currentMyEntityCount = new Dictionary<Model.EntityType, int>();
        Dictionary<Model.EntityType, int> previousEntityCount = new Dictionary<Model.EntityType, int>();
        Dictionary<Model.EntityType, float> buildEntityPriority = new Dictionary<Model.EntityType, float>();

        int howMuchResourcesIHaveNextTurn = 0;
        int nextTurnResourcesSelectCount = 5;
        int nextTurnResourcesBonus = 5;
        int howMuchResourcesCollectLastTurn = 0;
        const int howManyTurnsHistory = 30;
        int[] howMuchResourcesCollectLastNTurns = new int[howManyTurnsHistory];
        int[] howMuchResourcesCollectCPALastNTurns = new int[howManyTurnsHistory];
        int[] howMuchLiveBuildersLast10Turns = new int[howManyTurnsHistory];
        int howMuchResourcesCollectAll = 0;
        
        int myResources;
        int myScore;
        int myId;
        int mapSize;
        int populationMax = 0;
        int populationUsing = 0;
        #endregion
        #region Желания, Планы, Намерения и т.д.

        enum DesireType {WantCreateBuilders, WantCreateHouses, WantExtractResources };
        List<DesireType> desires = new List<DesireType>();
        List<DesireType> prevDesires = new List<DesireType>();
        
        enum PlanType {PlanCreateBuilders, PlanCreateHouses, PlanExtractResources }
        List<PlanType> plans = new List<PlanType>();
        List<PlanType> prevPlans = new List<PlanType>();
        
        enum IntentionType { IntentionCreateBuilder, IntentionStopCreatingBuilder, IntentionCreateHouses, IntentionExtractResources, IntentionFindResources }
        struct Intention
        {
            public IntentionType intentionType;
            public int targetId;

            public Intention(IntentionType type, int _targetId)
            {
                intentionType = type;
                targetId = _targetId;
            }
        }
        List<Intention> intentions = new List<Intention>();
        List<Intention> prevIntentions = new List<Intention>();

        Dictionary<int, Model.EntityAction> actions = new Dictionary<int, Model.EntityAction>();
        #endregion

        public Action GetAction(PlayerView playerView, DebugInterface debugInterface)
        {
            _playerView = playerView;
            #region first initialization arrays and lists (once)
            if (needPrepare == true)
            {
                //once prepare variables and struct
                myId = _playerView.MyId;
                mapSize = _playerView.MapSize;
                properties = _playerView.EntityProperties;
                needPrepare = false;
                Prepare();
            }
            #endregion

            #region calc statistics and informations
            foreach (var p in _playerView.Players)
            {
                if (p.Id == myId)
                {

                    howMuchResourcesCollectLastTurn = p.Resource - myResources; // не учитывает стоимость произведенных сущностей
                    myResources = p.Resource;
                    myScore = p.Score;
                    break;
                }
            }
            CountNumberOfEntitiesAndMap();
            CheckAliveAndDieEntities();
            #endregion

            GenerateDesires(); // Желания - Что я хочу сделать?       
            ConvertDesiresToPlans(); // Планы - Какие из желаний я могу сейчас сделать?
            ConvertPlansToIntentions(); // Намерения - Как и кем я буду выполнять планы?
            CorrectCrossIntentions();// Проверяем взаимоискулючающие и противоречащие намерения. Оставляем только нужные.
            ConvertIntentionsToActions(); // Приказы - Кто будет выполнять намерения?
            //приказы превращаются в конкретные action для entities

            //old logics
            // FindBuildPriorities();
            //CheckEntitiesNeedRepair();
            //CheckEntitiesGroup();

            //var actions = GenerateActions();

            //save previous entity state
            //SaveEntitiesMemory();

            return new Action(actions);
        }

        Dictionary<int, EntityAction> GenerateActions()
        {
            var actions = new Dictionary<int, Model.EntityAction>();

            EntityType entityType;
            EntityProperties property;

            //////================= BUILDER BASE ================ actions
            ////entityType = EntityType.BuilderBase;
            ////property = properties[entityType];
            ////foreach (var id in basicEntityIdGroups[entityType].members)
            ////{
            ////    if (buildEntityPriority[EntityType.RangedUnit] <= buildEntityPriority[EntityType.BuilderUnit]
            ////        && buildEntityPriority[EntityType.BuilderUnit] > 0)
            ////    {
            ////        BuildAction buildAction = new BuildAction();

            ////        Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, false);

            ////        buildAction.EntityType = property.Build.Value.Options[0];
            ////        buildAction.Position = target;

            ////        actions.Add(id, new EntityAction(null, buildAction, null, null));
            ////    } else
            ////    {
            ////        actions.Add(id, new EntityAction(null, null, null, null));
            ////    }
            ////}

            //================= RANGER BASE ============ actions
            entityType = EntityType.RangedBase;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (buildEntityPriority[EntityType.RangedUnit] > buildEntityPriority[EntityType.BuilderUnit]
                    && buildEntityPriority[EntityType.RangedUnit] > 0)
                {
                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, true);

                    BuildAction buildAction = new BuildAction();
                    buildAction.EntityType = property.Build.Value.Options[0];
                    buildAction.Position = target;

                    actions.Add(id, new EntityAction(null, buildAction, null, null));
                }
                else
                {
                    actions.Add(id, new EntityAction(null, null, null, null));
                }
            }

            //================= MELEE BASE ============ actions
            entityType = EntityType.MeleeBase;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (myResources > 600)
                {
                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, true);

                    BuildAction buildAction = new BuildAction();
                    buildAction.EntityType = property.Build.Value.Options[0];
                    buildAction.Position = target;

                    actions.Add(id, new EntityAction(null, buildAction, null, null));
                }
                else
                {
                    actions.Add(id, new EntityAction(null, null, null, null));
                }
            }

            //================== basic BUILDER UNIT actions ===================
            entityType = EntityType.BuilderUnit;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = new Vec2Int(_playerView.MapSize - 1, _playerView.MapSize - 1);

                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { EntityType.Resource });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            //=================== HOUSE builder unit ========================
            for (var i=0; i < houseBuilderGroup.members.Count; )
            {
                int id = houseBuilderGroup.members[i];
                bool removed = false;

                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = entityMemories[id].movePos;
                                
                //build

                if (IsFreeCellsRange(
                    entityMemories[id].targetPos.X,
                    entityMemories[id].targetPos.Y,
                    entityMemories[id].targetPos.X + properties[entityMemories[id].targetEntityType].Size - 1,
                    entityMemories[id].targetPos.Y + properties[entityMemories[id].targetEntityType].Size - 1
                    ))
                {
                    BuildAction buildAction = new BuildAction(
                        entityMemories[id].targetEntityType,
                        entityMemories[id].targetPos
                        );
                    actions.Add(id, new EntityAction(moveAction, buildAction, null, null));
                } else
                {
                    //can't build
                    entityMemories[id].ResetTarget();
                    entityMemories[id].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
                    removed = true;
                    
                }
                
                if (!removed)
                    i++;
            }

            //=================== REPAIR builder unit ========================
            for (var i = 0; i < repairBuilderGroup.members.Count;)
            {
                int id = repairBuilderGroup.members[i];
                bool removed = false;
                
                int targetId = entityMemories[id].targetId;

                if (targetId >= 0)
                {
                    if (entityMemories.ContainsKey(targetId))
                    {
                        if (entityMemories[targetId].myEntity.Health == properties[entityMemories[targetId].myEntity.EntityType].MaxHealth)
                        {
                            entityMemories[id].ResetTarget();
                            entityMemories[id].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
                            removed = true;
                        }
                        else
                        {
                            //repair

                            MoveAction moveAction = new MoveAction();
                            moveAction.BreakThrough = false;
                            moveAction.FindClosestPosition = true;
                            moveAction.Target = entityMemories[targetId].myEntity.Position;

                            RepairAction repairAction = new RepairAction(entityMemories[id].targetId);
                            actions.Add(id, new EntityAction(moveAction, null, null, repairAction));
                        }
                    }
                    else
                    {
                        //target die
                        entityMemories[id].ResetTarget();
                        entityMemories[id].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
                        removed = true;
                    }
                }

                if (!removed)
                    i++;
            }

            //ranged UNIT actions
            entityType = EntityType.RangedUnit;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);

                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            // =============== MELEE UNIT actions
            entityType = EntityType.MeleeUnit;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);


                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            //=========== TURRET =========== actions
            entityType = EntityType.Turret;
            property = properties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(property.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(null, null, attackAction, null));
            }

            return actions;
        }
        void Prepare()
        {
            //init arrays
            cellWithIdAny = new int[mapSize][];
            cellWithIdOnlyBuilding = new int[mapSize][];
            onceVisibleMap = new int[mapSize][];
            for (var i = 0; i < mapSize; i++)
            {
                cellWithIdAny[i] = new int[mapSize];
                cellWithIdOnlyBuilding[i] = new int[mapSize];
                onceVisibleMap[i] = new int[mapSize];
            }

            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    // cellWithIdAny - zeroing before use
                    // cellWithIdOnlyBuilding - zeroing before use
                    onceVisibleMap[x][y] = 0; //update once
                }
            }

            //prepare current and previous entity counter
            foreach (var ent in entityTypesArray)
            {
                currentMyEntityCount.Add(ent, 0);
                previousEntityCount.Add(ent, 0);
                buildEntityPriority.Add(ent, 0f);
                basicEntityIdGroups.Add(ent, new Group());
            }
        }
        void CheckAliveAndDieEntities()
        {
            hasInactiveHouse = false;

            //uncheck memory
            foreach (var m in entityMemories)
            {
                m.Value.checkedNow = false;
            }
            //update and add live entity
            foreach (var e in _playerView.Entities)
            {
                //use only my entities
                if (e.PlayerId == myId)
                {
                    if (e.Active == false)
                    {
                        hasInactiveHouse = true;
                    }
                    if (entityMemories.ContainsKey(e.Id))
                    {
                        //update
                        entityMemories[e.Id].Update(e);
                        AddEntityViewToOnceVisibleMap(e.EntityType, e.Position.X, e.Position.Y);
                    }
                    else
                    {
                        //add my entity
                        var em = new EntityMemory(e);
                        em.SetGroup(basicEntityIdGroups[e.EntityType]);
                        entityMemories.Add(e.Id, em);
                        AddEntityViewToOnceVisibleMap(e.EntityType, e.Position.X, e.Position.Y);

                        howMuchResourcesCollectLastTurn += properties[e.EntityType].Cost + currentMyEntityCount[e.EntityType] - 1;

                        //check my units
                        if (properties[em.myEntity.EntityType].CanMove == false)
                        {
                            for(int i = 0; i < houseBuilderGroup.members.Count; )
                            {
                                int id = houseBuilderGroup.members[i];
                                bool removed = false;
                                if (entityMemories[id].targetPos.X == em.myEntity.Position.X 
                                    && entityMemories[id].targetPos.Y == em.myEntity.Position.Y)
                                {
                                    removed = true;
                                    entityMemories[id].SetGroup(repairBuilderGroup);
                                    entityMemories[id].SetTargetId(e.Id);
                                }

                                if (!removed)
                                    i++;
                            }
                        }
                    }
                }
            }

            //remove died entity
            foreach (var m in entityMemories)
            {
                if (m.Value.checkedNow == false)
                {
                    m.Value.Die();
                    entityMemories.Remove(m.Key);
                }
            }

            //statistics
            if (_playerView.CurrentTick == 0)
            {
                howMuchResourcesCollectLastTurn = 0;
            }
            howMuchResourcesCollectAll += howMuchResourcesCollectLastTurn;
            for(int i = howMuchResourcesCollectCPALastNTurns.Length - 1; i > 0; i--)
            {
                howMuchResourcesCollectLastNTurns[i] = howMuchResourcesCollectLastNTurns[i - 1];
                howMuchResourcesCollectCPALastNTurns[i] = howMuchResourcesCollectCPALastNTurns[i - 1];
                howMuchLiveBuildersLast10Turns[i] = howMuchLiveBuildersLast10Turns[i - 1];
            }
            howMuchResourcesCollectLastNTurns[0] = howMuchResourcesCollectLastTurn;
            int co = currentMyEntityCount[EntityType.BuilderUnit];
            howMuchLiveBuildersLast10Turns[0] = co;
            howMuchResourcesCollectCPALastNTurns[0] = (co == 0) ? 0 : howMuchResourcesCollectLastTurn * 100 / co;

            int sum = 0;
            for (int i = 0; i < nextTurnResourcesSelectCount; i++)
            {
                sum += howMuchResourcesCollectLastNTurns[0];
            }
            howMuchResourcesIHaveNextTurn = myResources + sum / nextTurnResourcesSelectCount + nextTurnResourcesBonus;
        }
        void GenerateDesires()
        {
            prevDesires.Clear();
            prevDesires = desires;
            desires = new List<DesireType>();
            desires.Add(DesireType.WantCreateHouses);
            desires.Add(DesireType.WantCreateBuilders);
            desires.Add(DesireType.WantExtractResources);
            //// retreat from enemies
            /// info units about dangers zone

            ////build units
            /// build builder
            /// build ranger
            /// build melee

            //// build bases
            /// build BuilderBase
            /// build RangedBase
            /// build MeleeBase
            /// build house
            /// build turret
            /// build wall

            //// attack enemies
            /// my builder charge enemy builders

            //// collect resources
        }
        void ConvertDesiresToPlans()
        {
            prevPlans.Clear();
            prevPlans = plans;
            plans = new List<PlanType>();
            //add new plans
            foreach(var d in desires)
            {
                switch (d)
                {
                    case DesireType.WantCreateBuilders:                        
                        //i have base
                        if (currentMyEntityCount[EntityType.BuilderBase] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.BuilderUnit].Cost + currentMyEntityCount[EntityType.BuilderUnit] - 1;
                            if (howMuchResourcesIHaveNextTurn >= newCost)
                            {
                                plans.Add(PlanType.PlanCreateBuilders);
                            }
                        }
                        
                        break;
                    case DesireType.WantCreateHouses:
                        //i have builders
                        if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        {
                            //i have resources
                            int newCost = properties[EntityType.House].Cost;
                            if (howMuchResourcesIHaveNextTurn >= newCost)
                            {
                                plans.Add(PlanType.PlanCreateHouses);
                            }
                        }
                        break;
                    case DesireType.WantExtractResources:
                        //i have builders
                        if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        {                            
                            plans.Add(PlanType.PlanExtractResources);
                        }
                        break;
                    default:
                        int k = 5;//unknown type
                        break;
                }
            }
        }
        void ConvertPlansToIntentions()
        {
            prevIntentions.Clear();
            prevIntentions = intentions;
            intentions = new List<Intention>();
            foreach (var d in plans)
            {
                switch (d)
                {
                    case PlanType.PlanCreateBuilders:
                        foreach (var id in basicEntityIdGroups[EntityType.BuilderBase].members)
                        {
                            intentions.Add(new Intention(IntentionType.IntentionCreateBuilder, id));
                        }
                        break;
                    case PlanType.PlanCreateHouses:
                        ////i have builders
                        //if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        //{
                        //    //i have resources
                        //    int newCost = properties[EntityType.House].Cost;
                        //    if (howMuchResourcesIHaveNextTurn >= newCost)
                        //    {
                        //        plans.Add(PlanType.PlanCreateHouses);
                        //    }
                        //}
                        break;
                    case PlanType.PlanExtractResources:
                        ////i have builders
                        //if (currentMyEntityCount[EntityType.BuilderUnit] > 0)
                        //{
                        //    plans.Add(PlanType.PlanExtractResources);
                        //}
                        break;
                    default:
                        int k = 5;// unknown type
                        break;
                }
            }
        }
        void CorrectCrossIntentions()
        {

            // cancel build 
            foreach (var pi in prevIntentions)
            {
                switch (pi.intentionType)
                {
                    case IntentionType.IntentionCreateBuilder:
                        foreach(var ni in intentions)
                        {                            
                            if (ni.intentionType == IntentionType.IntentionCreateBuilder)
                            {
                                if (pi.targetId == ni.targetId)
                                    break;
                            }
                        }
                        intentions.Add(new Intention(IntentionType.IntentionStopCreatingBuilder, pi.targetId));
                        break;
                } 
            }

        }
        void ConvertIntentionsToActions()
        {
            actions.Clear();

            foreach (var ni in intentions)
            {
                switch (ni.intentionType)
                {
                    case IntentionType.IntentionCreateBuilder:
                        ActCreateUnit(ni.targetId, false);
                        break;
                    case IntentionType.IntentionStopCreatingBuilder:
                        ActCancelAll(ni.targetId);
                        break;
                }
            }
        }
        void ActCreateUnit(int baseId, bool agressive)
        {
            BuildAction buildAction = new BuildAction();

            Vec2Int target = FindSpawnPosition(entityMemories[baseId].myEntity.Position.X, entityMemories[baseId].myEntity.Position.Y, agressive);

            buildAction.EntityType = properties[entityMemories[baseId].myEntity.EntityType].Build.Value.Options[0];
            buildAction.Position = target;

            actions.Add(baseId, new EntityAction(null, buildAction, null, null));
        }
        void ActCancelAll(int id)
        {
            actions.Add(id, new EntityAction(null, null, null, null));
        }

        void AddEntityViewToOnceVisibleMap(EntityType entityType, int sx, int sy)
        {
            int sightRange = properties[entityType].SightRange;
            int size = properties[entityType].Size;
            // с этой клетки еще не смотрели по сторонам (благодаря перемещению или новому зданию)
            if (onceVisibleMap[sx][sy] < sightRange)
            {
                int sxRight = sx + size - 1;
                int syUp = sy + size - 1;
                for (int si = 0; si < size; si++)
                {
                    //my base
                    for (int siy = 0; siy < size; siy++)
                    {
                        SetOnceVisibleMapSafe(sx + si, sy + siy, sightRange);
                    }

                    //straight
                    for (int di = 1; di < sightRange; di++)
                    {
                        int value = sightRange - di;
                        SetOnceVisibleMapSafe(sx - di, sy + si, value);// left
                        SetOnceVisibleMapSafe(sx + si, sy - di, value);// down
                        SetOnceVisibleMapSafe(sxRight + di, sy + si, value);// right
                        SetOnceVisibleMapSafe(sx + si, syUp + di, value);// up
                    }
                }
                //diagonal
                for (int aa = 1; aa < sightRange - 1; aa++)
                {
                    for (int bb = 1; bb < sightRange - aa; bb++)
                    {
                        int value = sightRange - aa - bb;
                        SetOnceVisibleMapSafe(sx - aa, sy - bb, value);//left-down
                        SetOnceVisibleMapSafe(sx - aa, syUp + bb, value);//left-up
                        SetOnceVisibleMapSafe(sxRight + aa, syUp + bb, value);//right-up
                        SetOnceVisibleMapSafe(sxRight + aa, sy - bb, value);//right-down
                    }
                }
            }
        }
        void SetOnceVisibleMapSafe(int x, int y, int value)
        {
            if (x >= 0 && x < mapSize && y >=0 && y < mapSize)
            {
                if (onceVisibleMap[x][y] < value)
                    onceVisibleMap[x][y] = value;
            }
        }

        void SaveEntitiesMemory()
        {
            foreach (var e in entityMemories)
            {
                e.Value.SavePrevState();
            }
        }
        void CheckEntitiesGroup()
        {
           
            //need build houses or not
            if (buildEntityPriority[EntityType.House] > 0)
            {
                if (myResources >= properties[EntityType.House].Cost)
                {
                    //need build houses
                    if (houseBuilderGroup.members.Count == 0)
                    {
                        //check available life builders
                        if (basicEntityIdGroups[EntityType.BuilderUnit].members.Count > 0)
                        {
                            //select new builder
                            TrySelectFreeBuilderForBuild(EntityType.House);
                        }
                    }
                }
            }
            //else
            //{
            //    //don't need build houses
            //    if (houseBuilderGroup.members.Count > 0)
            //    {
            //        //remove all builders
            //        while (houseBuilderGroup.members.Count > 0)
            //        {
            //            entityMemories[houseBuilderGroup.members[0]].SetGroup(basicEntityIdGroups[EntityType.BuilderUnit]);
            //        }
            //    }
            //}
            

            //check current repairs


            //check new repairs
            foreach (var buildingId in needRepairEntityIdList)
            {
                bool needFindRepair = true;
                foreach(var builderId in repairBuilderGroup.members)
                {
                    if (entityMemories[builderId].targetId == buildingId)
                    {
                        needFindRepair = false;
                        break;
                    }
                }
                if (needFindRepair)
                {
                    if (basicEntityIdGroups[EntityType.BuilderUnit].members.Count > 0)
                    {
                        int index = random.Next(basicEntityIdGroups[EntityType.BuilderUnit].members.Count);
                        int id = basicEntityIdGroups[EntityType.BuilderUnit].members[index];
                        entityMemories[id].SetGroup(repairBuilderGroup);
                        entityMemories[id].SetTargetId(buildingId);
                    }
                }
            }

        }

        Vec2Int FindSpawnPosition(int myX, int myY, bool agressive)
        {
            //find nearest enemy
            int cx = myX + 2;
            int cy = myY + 2;

            Vec2Int targetPos;
            if (agressive)
            {
                targetPos = FindNearestEnemy(cx, cy);
            } else
            {
                targetPos = FindNearestResource(cx, cy);
            }

            int tx = 0;
            int ty = 0;

            //find nearest building cell
            int positionDindex = FindBuildingDPositionToTarget(myX, myY, targetPos.X, targetPos.Y);

            //if needed select nearest free 
            for (int i = 0; i < 20; i++)
            {
                int index = positionDindex + buildingPositionIter[i];
                if (index < 0) index += 20;
                if (index >= 20) index -= 20;

                int kx = myX + buildingPositionDX[index];
                int ky = myY + buildingPositionDY[index];

                if (kx >= 0
                    && kx < _playerView.MapSize
                    && ky >= 0
                    && ky < _playerView.MapSize)
                {
                    if (cellWithIdAny[kx][ky] < 0)
                    {
                        tx = kx;
                        ty = ky;
                        break;
                    }
                }
            }
            return new Vec2Int(tx, ty);
        }

        int FindBuildingDPositionToTarget(int bx, int by, int tx, int ty)
        {
            bool xLeft = false;
            bool xMiddle = false;
            bool xRight = false;
            if (tx < bx)
                xLeft = true;
            else if (tx >= bx + largeBuildingSize)
                xRight = true;
            else
                xMiddle = true;
            
            bool yUp = false;
            bool yDown = false;
            bool yCenter = false;
            if (ty < by)
                yDown = true;
            else if (ty >= by + largeBuildingSize)
                yUp = true;
            else
                yCenter = true;

            int targetD = -1;

            if (yCenter)
            {
                if (xLeft)
                {
                    targetD = ty - by;//0-4
                }
                if (xRight)
                {
                    targetD = 14 - ty + by; //14-10
                }
            }
            if (yDown)
            {
                if (xLeft)
                {
                    targetD = random.Next(2) * 19;//0 or 19
                }
                if (xMiddle)
                {
                    targetD = 19 - tx + bx ;//19-15
                }
                if (xRight)
                {
                    targetD = 14  + random.Next(2); //14-15
                }
            }

            if (yUp)
            {
                if (xLeft)
                {
                    targetD = 4 + random.Next(2);//4 or 5
                }
                if (xMiddle)
                {
                    targetD = 5 + tx - bx;//5-9
                }
                if (xRight)
                {
                    targetD = 9 + random.Next(2); //9-10
                }
            }

            if ( targetD < 0 || targetD >= 20)
            {
                return random.Next(20);
            }
            else
            {
                return targetD;
            }
                
        }

        Vec2Int FindNearestResource(int sx, int sy)
        {
            int index = -1;
            int distance = _playerView.MapSize * 3;

            for (int i = 0; i < _playerView.Entities.Length; i++)
            {
                if (_playerView.Entities[i].PlayerId == null)
                {                    
                    int d = System.Math.Abs(sx - _playerView.Entities[i].Position.X) + System.Math.Abs(sy - _playerView.Entities[i].Position.Y);
                    if (d < distance)
                    {
                        distance = d;
                        index = i;
                    }
                }
            }

            if (index >= 0)
            {
                return _playerView.Entities[index].Position;
            }
            else
            {
                return new Vec2Int(_playerView.MapSize / 2, _playerView.MapSize / 2);
            }
        }
        Vec2Int FindNearestEnemy(int sx, int sy)
        {
            int enemyIndex = -1;
            int distance = _playerView.MapSize * 3;
                                    
            for (int i = 0; i < _playerView.Entities.Length; i++)
            {
                if (_playerView.Entities[i].PlayerId != null)
                {
                    if (_playerView.Entities[i].PlayerId != myId)
                    {
                        int d = System.Math.Abs(sx - _playerView.Entities[i].Position.X) + System.Math.Abs(sy - _playerView.Entities[i].Position.Y);
                        if (d < distance)
                        {
                            distance = d;
                            enemyIndex = i;
                        }
                    }
                }
            }

            if (enemyIndex >= 0)
            {
                return _playerView.Entities[enemyIndex].Position;
            }else
            {
                return new Vec2Int(_playerView.MapSize / 2, _playerView.MapSize / 2);
            }
        }
        bool IsFreeCellsRange(int x1, int y1, int x2, int y2, bool onlyBuildings = false, bool ignoreBorder = false)
        {
            //xy1 always <= xy2
            if (x2 < x1) { int k = x1; x1 = x2; x2 = k; }
            if (y2 < y1) { int k = y1; y1 = y2; y2 = k; } 

            //check map border
            if (ignoreBorder)
            {
                if (x1 < 0) x1 = 0;
                if (y1 < 0) y1 = 0;
                if (x2 < 0) x2 = 0;
                if (y2 < 0) y2 = 0;
                if (x1 >= _playerView.MapSize) x1 = _playerView.MapSize - 1;
                if (y1 >= _playerView.MapSize) y1 = _playerView.MapSize - 1;
                if (x2 >= _playerView.MapSize) x2 = _playerView.MapSize - 1;
                if (y2 >= _playerView.MapSize) y2 = _playerView.MapSize - 1;
            }
            else
            {
                if (x1 < 0
                    || y1 < 0
                    || x2 < 0
                    || y2 < 0
                    || x1 >= _playerView.MapSize
                    || y1 >= _playerView.MapSize
                    || x2 >= _playerView.MapSize
                    || y2 >= _playerView.MapSize)
                {
                    return false;
                }
            }

            if (onlyBuildings)
            {
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        if (cellWithIdOnlyBuilding[x][y] >= 0) return false;
                    }
                }
            }
            else
            {
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        if (cellWithIdAny[x][y] >= 0) return false;
                    }
                }
            }            
            return true;
        }

        bool TryFindSpawnPlace(ref int sx, ref int sy, int size, bool horizontal)//horizontal is [[[]]]
        {
            //check map borders
            if (sx < 0 
                || sy < 0 
                || sx >= _playerView.MapSize
                || sy >= _playerView.MapSize)
            {
                return false;
            }

            //check first line
            if (!IsFreeCellsRange(sx, sy, (horizontal)? sx : (sx + size - 1), (horizontal) ? (sy + size - 1):sy))
            {
                return false;
            }


            //find minimun
            int minimum = 0;
            for (int i = 1; i < size; i++)
            {
                if (horizontal)
                {
                    if (!IsFreeCellsRange(sx - i, sy, sx - i, sy + size - 1))
                        break;                    
                }
                else
                {
                    if (!IsFreeCellsRange(sx, sy - i, sx + size - 1, sy - i))
                        break;                    
                }
                minimum = -i;
            }


            //check
            if ((1 - minimum) == size)
            {
                //sx;
                //sy;
                return true;
            }

            //find maximum
            for (int i = 1; i < size; i++)
            {
                if (horizontal)
                {
                    if (!IsFreeCellsRange(sx + i, sy, sx + i, sy + size - 1)) break;
                }
                else
                {
                    if (!IsFreeCellsRange(sx, sy + i, sx + size - 1, sy + i)) break;
                }

                if ((1 + i - minimum) == size)
                {
                    if (horizontal)
                    {
                        sx += minimum;
                    } else
                    {
                        sy += minimum;
                    }
                    return true;
                }
            }

            //                                      xo2yo2
            //          *       *       *    xi2yi2         maxDY = 2
            //          *       *       *       *             |
            //          *       *       *       *    pos      |
            //       xi1xi2     *       *       *           minDY = -1
            // xo1yo1
            // inner - almost free cells
            // outer - without buildings (can be units)

            return false;
        }

        void TrySelectFreeBuilderForBuild(EntityType buildingType)
        {
            int buildingSize = properties[buildingType].Size;
            foreach(var id in basicEntityIdGroups[EntityType.BuilderUnit].members)
            {
                Vec2Int pos = entityMemories[id].myEntity.Position;
                bool posFinded = false;

                //left 
                int x = pos.X - buildingSize;
                int y = pos.Y;
                if (TryFindSpawnPlace(ref x, ref y, buildingSize, false))
                {
                    posFinded = true;
                }
                else
                {
                    //down
                    x = pos.X;
                    y = pos.Y - buildingSize;
                    if (TryFindSpawnPlace(ref x, ref y, buildingSize, true))
                    {
                        posFinded = true;
                    }
                    else
                    {
                        //right
                        x = pos.X+1;
                        y = pos.Y;
                        if (TryFindSpawnPlace(ref x, ref y, buildingSize, false))
                        {
                            posFinded = true;
                        }
                        else
                        {
                            //up
                            x = pos.X;
                            y = pos.Y + 1;
                            if (TryFindSpawnPlace(ref x, ref y, buildingSize, true))
                            {
                                posFinded = true;
                            }
                        }
                    }
                }

                if (posFinded)
                {
                    entityMemories[id].SetGroup(houseBuilderGroup);                    
                    entityMemories[id].SetTargetPos(new Vec2Int(x, y));
                    entityMemories[id].SetMovePos(entityMemories[id].myEntity.Position);
                    entityMemories[id].SetTargetEntityType(EntityType.House);
                    break;
                }
            }
        }

        Color colorRed = new Color(1, 0, 0, 1);
        Color colorGreen = new Color(0, 1, 0, 1);
        Color colorBlue = new Color(0, 0, 1, 1);
        public void DebugUpdate(PlayerView playerView, DebugInterface debugInterface)
        {
            debugInterface.Send(new DebugCommand.Clear());
            DebugState debugState = debugInterface.GetState();

            #region draw CPA
            string text = $"{howMuchResourcesCollectAll} all";
            int textSize = 16;
            int margin = 2;
            int sx = debugState.WindowSize.X - 10;
            int sy = debugState.WindowSize.Y - 100;
            int index = 0;
            int col1 = 27;
            int col2 = 55;
            DebugData.PlacedText cpa = new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx, sy - index * (textSize + margin)), colorGreen), text, 1, textSize);
            debugInterface.Send(new DebugCommand.Add(cpa));
            index++;
            cpa = new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx, sy - index * (textSize + margin)), colorGreen), "add / bui / cpa", 1, textSize);
            debugInterface.Send(new DebugCommand.Add(cpa));

            for (int i = 0; i < howMuchResourcesCollectLastNTurns.Length; i++)
            {
                index++;
                debugInterface.Send(new DebugCommand.Add(
                    new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx - col2, sy - index * (textSize + margin)), colorGreen),
                    howMuchResourcesCollectLastNTurns[i] + " / ", 1, textSize)));
                debugInterface.Send(new DebugCommand.Add(
                    new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx - col1, sy - index * (textSize + margin)), colorGreen),
                    howMuchLiveBuildersLast10Turns[i] + " / ", 1, textSize)));
                debugInterface.Send(new DebugCommand.Add(
                    new DebugData.PlacedText(new ColoredVertex(null, new Vec2Float(sx, sy - index * (textSize + margin)), colorGreen),
                    howMuchResourcesCollectCPALastNTurns[i] + "", 1, textSize)));
            }
            #endregion


            //if (playerView.CurrentTick == 10)
            //{
            //    debugInterface.Send(new DebugCommand.Add(new DebugData.Log("Тестовое сообщение")));

            //    ColoredVertex position = new ColoredVertex(null, new Vec2Float(10, 10), colorGreen);
            //    DebugData.PlacedText text = new DebugData.PlacedText(position, "Ghbdtn", 0, 16);
            //    debugInterface.Send(new DebugCommand.Add(text));

            //    ColoredVertex[] vertices = new ColoredVertex[] {
            //        new ColoredVertex(new Vec2Float(7,7), new Vec2Float(), colorRed),
            //        new ColoredVertex(new Vec2Float(17,7), new Vec2Float(), colorRed),
            //        new ColoredVertex(new Vec2Float(20,20), new Vec2Float(), colorRed),
            //        new ColoredVertex(new Vec2Float(10,10), new Vec2Float(), colorRed)
            //    };
            //    DebugData.Primitives lines = new DebugData.Primitives(vertices, PrimitiveType.Lines);
            //    debugInterface.Send(new DebugCommand.Add(lines));
            //}
        }

        void CountNumberOfEntitiesAndMap()
        {           
            //clear map
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    cellWithIdAny[x][y] = -1;
                    cellWithIdOnlyBuilding[x][y] = -1;
                }
            }

            //save previous number of entities
            foreach(var p in previousEntityCount)
            {
                previousEntityCount[p.Key] = currentMyEntityCount[p.Key];
            }

            //zeroize current enity count
            foreach (var ent in entityTypesArray)
            {
                currentMyEntityCount[ent] = 0;
            }
            //count current entities
            foreach (var entity in _playerView.Entities)
            {
                //fill freeCell arraies for map of moving
                int size = properties[entity.EntityType].Size;
                int x1 = entity.Position.X;
                int x2 = x1 + size - 1;
                int y1 = entity.Position.Y;
                int y2 = y1 + size - 1;
                bool canMove = properties[entity.EntityType].CanMove;
                int id = entity.Id;
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        //this is building
                        if (!canMove) 
                            cellWithIdOnlyBuilding[x][y] = id;
                        //this is any enitites
                        cellWithIdAny[x][y] = id;
                    }
                }                   

                if (entity.PlayerId == myId)
                {
                    //count number of my enitites
                    currentMyEntityCount[entity.EntityType]++;
                } 
            }
            //calc max and current population
            populationMax = 0;
            populationUsing = 0;
            foreach (var e in properties)
            {
                populationMax += currentMyEntityCount[e.Key] * e.Value.PopulationProvide;
                populationUsing += currentMyEntityCount[e.Key] * e.Value.PopulationUse;
            }

            //theoreticaly same population using
            //unitCount = currentMyEntityCount[EntityType.BuilderUnit] + currentMyEntityCount[EntityType.MeleeUnit] + currentMyEntityCount[EntityType.RangedUnit];
        }
        void CheckEntitiesNeedRepair()
        {
            needRepairEntityIdList.Clear();

            EntityType entityType = EntityType.BuilderBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < properties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }

            entityType = EntityType.RangedBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < properties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
            entityType = EntityType.Turret;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < properties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
            entityType = EntityType.House;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < properties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
            entityType = EntityType.MeleeBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < properties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
        }
        void FindBuildPriorities()
        {
            foreach (var ent in entityTypesArray)
            {
                buildEntityPriority[ent] = FindBuildEntityPriority(ent);
            }
        }
        float GetTargetEntityCount(EntityType entityType)
        {
            switch (entityType)
            {
                case EntityType.BuilderBase:
                    if (currentMyEntityCount[entityType] == 0)
                        return 1f;
                    break;
                case EntityType.RangedBase:
                    if (currentMyEntityCount[entityType] == 0)
                        return 1f;
                    break;
                case EntityType.BuilderUnit:
                    if (populationUsing == populationMax)
                        return 0f;
                    for (int i = 0; i < maxTargetCountTiers.Length; i++)
                    {
                        if (populationUsing <= maxTargetCountTiers[i])
                        {
                            return populationMax * builderTargetCountTiers[i];
                        }
                    }
                    return populationMax * builderTargetCountTiers[builderTargetCountTiers.Length - 1];
                case EntityType.RangedUnit:
                    if (populationUsing == populationMax)
                        return 0f;
                    for (int i = 0; i < maxTargetCountTiers.Length; i++)
                    {
                        if (populationUsing <= maxTargetCountTiers[i])
                        {
                            return populationMax * rangerTargetCountTiers[i];
                        }
                    }
                    return populationMax * rangerTargetCountTiers[rangerTargetCountTiers.Length - 1];
                case EntityType.House:
                    if (populationMax - populationUsing < housePopulationDelay)
                        return currentMyEntityCount[entityType] + 1f;
                    break;
            }
            return 0f;
        }

        float FindBuildEntityPriority(EntityType entityType)
        {
            float targetCount = GetTargetEntityCount(entityType);

            switch (entityType)
            {

                case EntityType.MeleeUnit:
                    return 0f;
                case EntityType.BuilderUnit:
                case EntityType.RangedUnit:                    
                    return 1f - currentMyEntityCount[entityType] / targetCount;
                case EntityType.House:
                    if (targetCount != 0)
                        return 1 - (populationMax - populationUsing) / housePopulationDelay;
                    break;
                case EntityType.BuilderBase:
                    return targetCount * 10f;//0 or 10
                case EntityType.RangedBase:
                    return targetCount * 5f;//0 or 5
            }

            return 0f;
        }

    }

    class EntityMemory
    {
        public Group group { get; private set; }
        public int prevHealth { get; private set; }
        public Vec2Int prevPosition { get; private set; }
        public int myId { get; private set; }
        public int targetId { get; private set; }
        public Vec2Int targetPos { get; private set; }
        public Vec2Int movePos { get; private set; }
        public EntityType targetEntityType { get; private set; }

        public Entity myEntity { get; private set; } 

        public bool checkedNow;

        //EntityMemory()
        //{
        //    group = null;
        //    prevHealth = -1;
        //    prevPosition = new Vec2Int(-1, -1);
        //    myId = -1;
        //    checkedNow = false;
        //}

        public EntityMemory(Entity entity)
        {
            group = null;
            prevHealth = entity.Health;
            prevPosition = entity.Position;
            myId = entity.Id;
            myEntity = entity;
            checkedNow = true;
            targetId = -1;
            targetPos = new Vec2Int(-1, -1);
            movePos = new Vec2Int(-1, -1);
        }
        public void Update(Entity entity)
        {
            checkedNow = true;
            myEntity = entity;
        }

        public void ResetTarget()
        {
            targetId = -1;
            targetPos = new Vec2Int(-1, -1);
            movePos = new Vec2Int(-1, -1);
        }
        public void SetGroup(Group g)
        {
            if (group != null)
            {
                group.RemoveMember(myId);
            }
            group = g;
            group.AddMember(myId);
        }

        public void SetTargetId(int id)
        {
            targetId = id;
        }
        public void SetTargetPos(Vec2Int vec)
        {
            targetPos = vec;
        }
        public void SetMovePos(Vec2Int vec)
        {
            movePos = vec;
        }
        public void SetTargetEntityType(EntityType entityType)
        {
            targetEntityType = entityType;
        }
        public void SavePrevState ()
        {
            prevHealth = myEntity.Health;
            prevPosition = myEntity.Position;
        }
        public void Die()
        {
            if (group != null)
            {
                group.RemoveMember(myId);
            }
        }
    }

    class Group{

        public List<int> members { get; private set; }

        public Group()
        {
            members = new List<int>();
        }

        public void AddMember(int m)
        {
            if (!members.Contains(m))
            {
                members.Add(m);
            }
        }

        public void RemoveMember(int m)
        {
            if (members.Contains(m))
            {
                members.Remove(m);
            }
        }
    }
}
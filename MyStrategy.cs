using Aicup2020.Model;
using System.Collections.Generic;

namespace Aicup2020
{
    public class MyStrategy
    {
        Dictionary<Model.EntityType, int> currentMyEntityCount = new Dictionary<Model.EntityType, int>();
        Dictionary<Model.EntityType, int> previousEntityCount = new Dictionary<Model.EntityType, int>();
        Dictionary<Model.EntityType, float> buildEntityPriority = new Dictionary<Model.EntityType, float>();
        Dictionary<int, EntityMemory> entityMemories = new Dictionary<int, EntityMemory>();

        EntityType[] entityTypesArray = { EntityType.BuilderBase, EntityType.BuilderUnit, EntityType.House, EntityType.MeleeBase, EntityType.MeleeUnit, EntityType.RangedBase, EntityType.RangedUnit, EntityType.Resource, EntityType.Turret, EntityType.Wall };

        int populationMax = 0;
        int populationUsing = 0;

        bool needPrepare = true;

        int[] maxTargetCountTiers = { 5, 15, 40, 80};
        float[] builderTargetCountTiers = { 1f, 0.75f, 0.7f, 0.6f , 0.3f};
        float[] rangerTargetCountTiers = { 0f, 0.25f, 0.3f, 0.4f, 0.7f };
        float housePopulationDelay = 5f;

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

        Dictionary<EntityType, Group> basicEntityIdGroups = new Dictionary<EntityType, Group>();
        Group houseBuilderGroup = new Group();
        Group repairBuilderGroup = new Group();
        List<int> needRepairEntityIdList = new List<int>();
        bool hasInactiveHouse = false;

        PlayerView _playerView;
        System.Random random = new System.Random();
        int[][] cellWithIdOnlyBuilding;
        int[][] cellWithIdAny;
        int myResources;
        int myScore;
        int myId;

        public Action GetAction(PlayerView playerView, DebugInterface debugInterface)
        {
            _playerView = playerView;
            if (needPrepare == true)
            {
                //once prepare variables and struct
                needPrepare = false;
                Prepare();
            }
            myId = _playerView.MyId;
            foreach (var p in _playerView.Players)
            {
                if (p.Id == myId)
                {
                    myResources = p.Resource;
                    myScore = p.Score;
                }
            }

            //prepare
            CheckEntitiesCount();            
            CheckEntitiesMemory();

            //analyze
            FindBuildPriorities();

            CheckEntitiesNeedRepair();

            CheckEntitiesGroup();

            var actions = SelectActions();
     
            //save previous entity state
            SaveEntitiesMemory();

            return new Action(actions);
        }

        Dictionary<int, EntityAction> SelectActions()
        {
            var actions = new Dictionary<int, Model.EntityAction>();

            EntityType entityType;
            EntityProperties properties;

            //================= BUILDER BASE ================ actions
            entityType = EntityType.BuilderBase;
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (buildEntityPriority[EntityType.RangedUnit] <= buildEntityPriority[EntityType.BuilderUnit]
                    && buildEntityPriority[EntityType.BuilderUnit] > 0)
                {
                    BuildAction buildAction = new BuildAction();

                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, false);

                    buildAction.EntityType = properties.Build.Value.Options[0];
                    buildAction.Position = target;

                    actions.Add(id, new EntityAction(null, buildAction, null, null));
                } else
                {
                    actions.Add(id, new EntityAction(null, null, null, null));
                }
            }

            //================= RANGER BASE ============ actions
            entityType = EntityType.RangedBase;
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (buildEntityPriority[EntityType.RangedUnit] > buildEntityPriority[EntityType.BuilderUnit]
                    && buildEntityPriority[EntityType.RangedUnit] > 0)
                {
                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, true);

                    BuildAction buildAction = new BuildAction();
                    buildAction.EntityType = properties.Build.Value.Options[0];
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
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (myResources > 600)
                {
                    Vec2Int target = FindSpawnPosition(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y, true);

                    BuildAction buildAction = new BuildAction();
                    buildAction.EntityType = properties.Build.Value.Options[0];
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
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = new Vec2Int(_playerView.MapSize - 1, _playerView.MapSize - 1);

                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(properties.SightRange, new EntityType[] { EntityType.Resource });

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
                    entityMemories[id].targetPos.X + _playerView.EntityProperties[entityMemories[id].targetEntityType].Size - 1,
                    entityMemories[id].targetPos.Y + _playerView.EntityProperties[entityMemories[id].targetEntityType].Size - 1
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
                        if (entityMemories[targetId].myEntity.Health == _playerView.EntityProperties[entityMemories[targetId].myEntity.EntityType].MaxHealth)
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
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);

                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(properties.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            // =============== MELEE UNIT actions
            entityType = EntityType.MeleeUnit;
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                MoveAction moveAction = new MoveAction();
                moveAction.BreakThrough = true;
                moveAction.FindClosestPosition = true;
                moveAction.Target = FindNearestEnemy(entityMemories[id].myEntity.Position.X, entityMemories[id].myEntity.Position.Y);


                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(properties.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(moveAction, null, attackAction, null));
            }

            //=========== TURRET =========== actions
            entityType = EntityType.Turret;
            properties = _playerView.EntityProperties[entityType];
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                AttackAction attackAction = new AttackAction();
                attackAction.AutoAttack = new AutoAttack(properties.SightRange, new EntityType[] { });

                actions.Add(id, new EntityAction(null, null, attackAction, null));
            }

            return actions;
        }

        void CheckEntitiesMemory()
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
                if (e.PlayerId == _playerView.MyId)
                {
                    if (e.Active == false)
                    {
                        hasInactiveHouse = true;
                    }
                    if (entityMemories.ContainsKey(e.Id))
                    {
                        //update

                        entityMemories[e.Id].Update(e);
                    }
                    else
                    {
                        //add
                        var em = new EntityMemory(e);
                        em.SetGroup(basicEntityIdGroups[e.EntityType]);
                        entityMemories.Add(e.Id, em);

                        //check my builder
                        if (_playerView.EntityProperties[em.myEntity.EntityType].CanMove == false)
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
                if (myResources >= _playerView.EntityProperties[EntityType.House].Cost)
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
            int buildingSize = _playerView.EntityProperties[buildingType].Size;
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
        public void DebugUpdate(PlayerView playerView, DebugInterface debugInterface)
        {
            debugInterface.Send(new DebugCommand.Clear());
            debugInterface.GetState();
        }
        void Prepare()
        {
            int mapSize = _playerView.MapSize;
            cellWithIdAny = new int[mapSize][];
            cellWithIdOnlyBuilding = new int[mapSize][];
            for(var i = 0; i < mapSize; i++)
            {
                cellWithIdAny[i] = new int[mapSize];
                cellWithIdOnlyBuilding[i] = new int[mapSize];
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
        void CheckEntitiesCount()
        {
            int myId = _playerView.MyId;
            int mapSize = _playerView.MapSize;

            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    cellWithIdAny[x][y] = -1;
                    cellWithIdOnlyBuilding[x][y] = -1;
                }
            }

            previousEntityCount = currentMyEntityCount;
            //zeroize current enity count
            foreach (var ent in entityTypesArray)
            {
                currentMyEntityCount[ent] = 0;
            }
            //count current entities
            foreach (var entity in _playerView.Entities)
            {
                //fill freeCell arraies
                int size = _playerView.EntityProperties[entity.EntityType].Size;
                int x1 = entity.Position.X;
                int x2 = x1 + size - 1;
                int y1 = entity.Position.Y;
                int y2 = y1 + size - 1;
                bool canMove = _playerView.EntityProperties[entity.EntityType].CanMove;
                int id = entity.Id;
                for (int x = x1; x <= x2; x++)
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        if (!canMove) 
                            cellWithIdOnlyBuilding[x][y] = id;

                        cellWithIdAny[x][y] = id;
                    }
                }                   

                if (entity.PlayerId == myId)
                {
                    currentMyEntityCount[entity.EntityType]++;
                }
            }
            //calc max and current population
            populationMax = 0;
            populationUsing = 0;
            foreach (var e in _playerView.EntityProperties)
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
                if (entityMemories[id].myEntity.Health < _playerView.EntityProperties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }

            entityType = EntityType.RangedBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < _playerView.EntityProperties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
            entityType = EntityType.Turret;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < _playerView.EntityProperties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
            entityType = EntityType.House;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < _playerView.EntityProperties[entityType].MaxHealth)
                {
                    needRepairEntityIdList.Add(id);
                }
            }
            entityType = EntityType.MeleeBase;
            foreach (var id in basicEntityIdGroups[entityType].members)
            {
                if (entityMemories[id].myEntity.Health < _playerView.EntityProperties[entityType].MaxHealth)
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
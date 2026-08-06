using System.Collections.Generic;
using Engine;
using Game;

namespace TreeChopperMod {
    // Mod baseado no hook OnBlockDug, que dispara quando o jogador TERMINA
    // de escavar um bloco (ComponentMiner.cs, logo após a chamada de DestroyCell).
    // Isso evita lidar com OnMinerDig (que roda todo frame durante a escavação)
    // e evita recursão, já que chamamos DestroyCell manualmente aqui, fora do
    // fluxo normal de ChangeCell/TerrainChangeCell.
    public class TreeChopperModLoader : ModLoader {

        // Limite de segurança: evita árvores gigantes (ou builds maliciosas)
        // travarem o jogo derrubando milhares de blocos de uma vez.
        private const int MaxBlocksPerChop = 256;

        public override void __ModInitialize() {
            ModsManager.RegisterHook("OnBlockDug", this);
        }

        public override void OnBlockDug(ComponentMiner componentMiner,
            BlockPlacementData digValue,
            int cellValue,
            ref int durabilityReduction,
            ref bool mute,
            ref int playerDataAdd) {

            int contents = Terrain.ExtractContents(cellValue);
            Block diggedBlock = BlocksManager.Blocks[contents];

            // Só reage se o bloco escavado for tronco (WoodBlock é a classe-base
            // de OakWoodBlock, BirchWoodBlock, SpruceWoodBlock etc.)
            if (diggedBlock is not WoodBlock) {
                return;
            }

            SubsystemTerrain subsystemTerrain = componentMiner.m_subsystemTerrain;
            Point3 origin = new Point3(digValue.CellFace.X, digValue.CellFace.Y, digValue.CellFace.Z);

            ChopConnectedWood(subsystemTerrain, origin, contents);
        }

        // BFS flood-fill: a partir do bloco recém-cortado, encontra todos os
        // blocos de madeira do MESMO tipo (mesmo BlockIndex) conectados
        // ortogonalmente (6 direções) e derruba todos.
        private void ChopConnectedWood(SubsystemTerrain subsystemTerrain, Point3 origin, int woodContents) {
            Queue<Point3> queue = new Queue<Point3>();
            HashSet<Point3> visited = new HashSet<Point3> { origin };
            List<Point3> toChop = new List<Point3>();

            queue.Enqueue(origin);

            while (queue.Count > 0 && toChop.Count < MaxBlocksPerChop) {
                Point3 current = queue.Dequeue();

                foreach (Point3 neighbor in GetNeighbors(current)) {
                    if (visited.Contains(neighbor)) {
                        continue;
                    }
                    visited.Add(neighbor);

                    int neighborValue = subsystemTerrain.Terrain.GetCellValue(neighbor.X, neighbor.Y, neighbor.Z);
                    int neighborContents = Terrain.ExtractContents(neighborValue);

                    // Só continua o flood-fill por troncos do MESMO tipo de árvore.
                    // (Troca "neighborContents == woodContents" por
                    // "BlocksManager.Blocks[neighborContents] is WoodBlock" se quiser
                    // que espécies diferentes coladas também caiam juntas.)
                    if (neighborContents == woodContents) {
                        toChop.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Derruba tudo que foi encontrado, um DestroyCell por célula,
            // com toolLevel alto pra garantir os drops de item (madeira).
            foreach (Point3 cell in toChop) {
                subsystemTerrain.DestroyCell(
                    int.MaxValue,
                    cell.X,
                    cell.Y,
                    cell.Z,
                    0,      // newValue = ar
                    false,  // noDrop = false -> dropa item
                    false   // noParticleSystem = false -> mostra partículas de destroço
                );
            }
        }

        private static IEnumerable<Point3> GetNeighbors(Point3 p) {
            yield return new Point3(p.X + 1, p.Y, p.Z);
            yield return new Point3(p.X - 1, p.Y, p.Z);
            yield return new Point3(p.X, p.Y + 1, p.Z);
            yield return new Point3(p.X, p.Y - 1, p.Z);
            yield return new Point3(p.X, p.Y, p.Z + 1);
            yield return new Point3(p.X, p.Y, p.Z - 1);
        }
    }
}

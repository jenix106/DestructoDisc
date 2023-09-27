using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace DestructoDisc
{
    public class DestructoDiscSpell : SpellCastCharge
    {
        Item disc;
        bool isShooting = false;
        public float projectileSpeedMultiplier = 15;
        public Color projectileColor = new Color(5, 5, 0, 1);
        public float projectileDespawnTimer = 10;
        public float projectileDamage = 10;
        public override void Fire(bool active)
        {
            base.Fire(active);
            if (active)
            {
                Catalog.GetData<ItemData>("DestructoDisc").SpawnAsync(item =>
                {
                    disc = item;
                    disc.renderers[0].material.SetColor("_BaseColor", projectileColor);
                    disc.gameObject.AddComponent<DestructoDiscComponent>().spellCaster = spellCaster;
                    disc.GetComponent<DestructoDiscComponent>().damage = projectileDamage;
                    disc.mainHandleRight.SetTouch(false);
                }, spellCaster.magic.position + (spellCaster.magic.forward * 0.1f), Quaternion.LookRotation(-spellCaster.magic.up, spellCaster.magic.forward), null, false);
            }
            else if (!active && disc != null && !isShooting)
            {
                disc.Despawn();
                disc = null;
            }
            else if (!active && disc != null && isShooting)
            {
                isShooting = false;
                disc.Despawn(projectileDespawnTimer);
                disc = null;
            }
        }
        public override void Throw(Vector3 velocity)
        {
            base.Throw(velocity);
            if(disc != null)
            {
                isShooting = true;
                disc.physicBody.AddForce(velocity * projectileSpeedMultiplier, ForceMode.Impulse);
                disc.Throw(1, Item.FlyDetection.Forced);
                disc.mainHandleRight.SetTouch(true);
                GameObject sound = new GameObject("Sound");
                sound.transform.position = disc.transform.position;
                EffectInstance shoot = Catalog.GetData<EffectData>("DestructoDiscLaunch").Spawn(sound.transform, null, true);
                shoot.SetIntensity(1f);
                shoot.Play();
                GameObject.Destroy(sound, 5);
            }
        }
        public override void UpdateCaster()
        {
            base.UpdateCaster();
            if (spellCaster.isFiring && disc != null)
            {
                disc.transform.position = spellCaster.magic.position + (spellCaster.magic.forward * 0.1f);
                disc.transform.rotation = Quaternion.LookRotation(-spellCaster.magic.up, spellCaster.magic.forward);
                disc.transform.localScale = Vector3.one * currentCharge;
            }
        }
    }
    public class DestructoDiscComponent : MonoBehaviour
    {
        Item item;
        public SpellCaster spellCaster;
        List<Creature> creatures = new List<Creature>();
        public float damage;
        public void Start()
        {
            item = GetComponent<Item>();
            item.mainCollisionHandler.OnTriggerEnterEvent += MainCollisionHandler_OnTriggerEnterEvent;
        }
        private void MainCollisionHandler_OnTriggerEnterEvent(Collider other)
        {
            if (!other.isTrigger)
            {
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, damage), Catalog.GetData<MaterialData>("Projectile"));
                collision.casterHand = spellCaster;
                collision.targetCollider = other;
                collision.targetColliderGroup = other.GetComponentInParent<ColliderGroup>();
                collision.sourceColliderGroup = item.colliderGroups[0];
                collision.sourceCollider = item.colliderGroups[0].colliders[0]; 
                if (other.GetComponentInParent<Breakable>() is Breakable breakable)
                {
                    if (item.physicBody.velocity.sqrMagnitude < breakable.neededImpactForceToDamage)
                        return;
                    float sqrMagnitude = item.physicBody.velocity.sqrMagnitude;
                    --breakable.hitsUntilBreak;
                    if (breakable.canInstantaneouslyBreak && sqrMagnitude >= breakable.instantaneousBreakVelocityThreshold)
                        breakable.hitsUntilBreak = 0;
                    breakable.onTakeDamage?.Invoke(sqrMagnitude);
                    if (breakable.IsBroken || breakable.hitsUntilBreak > 0)
                        return;
                    breakable.Break();
                }
                if (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<RagdollPart>() is RagdollPart part && part.ragdoll.creature != spellCaster.mana.creature)
                {
                    Vector3 direction = part.GetSliceDirection();
                    if (part.sliceAllowed && part.ragdoll.creature.player == null)
                    {
                        float num1 = Vector3.Dot(direction, item.transform.right);
                        float num2 = 1f/3f;
                        if (num1 < num2 && num1 > -num2)
                        {
                            part.ragdoll.TrySlice(part);
                            if (part.data.sliceForceKill) part.ragdoll.creature.Kill();
                        }
                    }
                    collision.damageStruct.hitRagdollPart = part;
                    if (!creatures.Contains(part.ragdoll.creature))
                    {
                        part.ragdoll.creature.Damage(collision);
                        if(!part.ragdoll.creature.isPlayer)
                        part.ragdoll.creature.TryPush(Creature.PushType.Hit, item.physicBody.velocity, 2, part.type);
                        creatures.Add(part.ragdoll.creature);
                    }
                }
                if (other.attachedRigidbody == null)
                {
                    EffectInstance instance = Catalog.GetData<EffectData>("HitDestructoDisc").Spawn(item.transform.position + (item.transform.forward * 0.2f), Quaternion.LookRotation(item.transform.forward, item.transform.right), other.transform, collision, true, null, false);
                    instance.SetIntensity(1f);
                    instance.Play();
                }
                else if (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<RagdollPart>()?.ragdoll?.creature != spellCaster.mana.creature)
                {
                    EffectInstance instance = Catalog.GetData<EffectData>("HitDestructoDiscNoDecal").Spawn(item.transform.position + (item.transform.forward * 0.2f), Quaternion.LookRotation(item.transform.forward, item.transform.right), other.transform, collision, true, null, false);
                    instance.SetIntensity(1f);
                    instance.Play();
                }
            }
        }
    }
}

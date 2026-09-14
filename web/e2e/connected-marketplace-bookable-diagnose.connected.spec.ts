import { expect, test, type Page } from '@playwright/test'
const tenantId='10000000-0000-0000-0000-000000000040'
const buyerUserId='10000000-0000-0000-0000-000000000001'
type Session={antiforgeryToken:string}
test('diagnose funded Marketplace bookability',async({page})=>{
 await page.goto('/sign-in'); await page.getByRole('button',{name:/Continue to Advertified/}).click(); await page.getByRole('button',{name:/Advertified Local/}).click();
 const s=await session(page); let r=await page.request.post('/api/v1/development/connected-acceptance/bootstrap',{data:{},headers:h(s.antiforgeryToken)}); expect(r.status(),await r.text()).toBe(200)
 const s2=await session(page); r=await page.request.post('/api/v1/development/connected-acceptance/identity',{data:{userId:buyerUserId},headers:h(s2.antiforgeryToken)}); expect(r.status(),await r.text()).toBe(200)
 const gets=async(path:string)=>{const x=await page.request.get(`/api/v1/tenants/${tenantId}/${path}`); expect(x.ok(),await x.text()).toBe(true); return await x.json()}
 const proposals=await gets('proposals') as any[]; const selected=proposals.filter(x=>/Connected Marketplace OOH Proposal/i.test(x.title)&&x.status==='SELECTED').sort((a,b)=>b.createdAtUtc.localeCompare(a.createdAtUtc))[0];
 const proposal=await gets(`proposals/${selected.id}`) as any; const option=proposal.options.find((x:any)=>x.id===proposal.decision?.optionId)
 const funding=await gets('funding'); const rfqs=await gets('marketplace-rfqs?pageSize=50'); const campaigns=await gets('campaigns'); const bookable=await gets('bookings/bookable-lines')
 console.log(JSON.stringify({proposalId:proposal.id,decision:proposal.decision,option:{id:option?.id,budgetMinor:option?.budgetMinor,inventory:option?.inventory?.map((x:any)=>({name:x.name,lineId:x.mediaPlanLineId,listing:x.marketplaceListingVersionId,qty:x.quantity,start:x.runningPeriods?.[0]?.start,end:x.runningPeriods?.at(-1)?.end,client:x.clientPriceMinor,fees:x.feesMinor,vat:x.vatMinor,supplier:x.clientPriceMinor-x.feesMinor-x.vatMinor,currency:x.currency}))},funding,rfqs:rfqs.items?.map((x:any)=>({id:x.id,listing:x.listingVersionId,start:x.requestedStart,end:x.requestedEnd,qty:x.quantity,status:x.status,response:x.response&&{amount:x.response.amountMinor,currency:x.response.currency,version:x.response.responseVersion,acceptedBy:x.response.acceptedBy}})),campaigns,bookable},null,2))
})
async function session(page:Page){const r=await page.request.get('/api/v1/session');expect(r.ok(),await r.text()).toBe(true);return await r.json() as Session}
function h(t:string){return{Origin:'http://localhost:3017','X-CSRF-TOKEN':t,'Idempotency-Key':crypto.randomUUID(),'X-Correlation-ID':crypto.randomUUID()}}

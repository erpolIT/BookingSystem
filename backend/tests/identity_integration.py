import json,uuid,secrets,http.cookiejar
from urllib.request import Request,build_opener,HTTPCookieProcessor
from urllib.error import HTTPError
from datetime import datetime,timedelta
from zoneinfo import ZoneInfo
from concurrent.futures import ThreadPoolExecutor
import os
base=os.environ.get('BOOKING_TEST_API', 'http://localhost:5130/api')
class Client:
 def __init__(self):
  self.cookies=http.cookiejar.CookieJar();self.opener=build_opener(HTTPCookieProcessor(self.cookies))
 def call(self,path,method='GET',data=None,headers=None,csrf=True):
  h={'Content-Type':'application/json',**(headers or {})}
  if method!='GET' and csrf:h['X-CSRF-TOKEN']=self.call('/auth/csrf')[1]['token']
  req=Request(base+path,method=method,data=json.dumps(data).encode() if data is not None else None,headers=h)
  try:
   with self.opener.open(req,timeout=30) as r:
    raw=r.read();return r.status,json.loads(raw) if raw else None
  except HTTPError as e:
   raw=e.read()
   try:body=json.loads(raw)
   except:body=None
   return e.code,body
anon=Client();status,_=anon.call('/bookings');assert status==401,('anonymous',status)
assert anon.call('/auth/register','POST',{'email':'blocked@example.com','password':'ExamplePassword!123'},csrf=False)[0]==400
print('Anonymous private requests: 401; registration without CSRF: 400',flush=True)
clients=[Client(),Client()];users=[]
for client in clients:
 email='identity-test-'+uuid.uuid4().hex+'@example.com';password='Test!Aa1'+secrets.token_hex(12)
 credentials={'email':email,'password':password}
 status,_=client.call('/auth/register','POST',credentials);assert status==201,('register',status)
 old_token=client.call('/auth/csrf')[1]['token']
 status,body=client.call('/auth/login','POST',credentials);assert status==200,('login',status)
 users.append(body['user'])
 assert client.call('/auth/me')[1]['user']['id']==body['user']['id']
 assert client.call('/auth/logout','POST',headers={'X-CSRF-TOKEN':old_token},csrf=False)[0]==400
 auth_cookie=next(c for c in client.cookies if c.name=='BookingSystem.Auth')
 assert any(key.lower() == 'httponly' for key in auth_cookie._rest)
 assert client.call('/providers','POST',{'name':'Unauthorized Writer'})[0]==403
 print('Registration/login, HttpOnly session, stale CSRF rejection, management denied: OK',flush=True)
a,b=clients
assert a.call('/bookings/1')[0]==404
assert a.call('/bookings/1/cancel','POST')[0]==404
print('Legacy booking without owner inaccessible: 404',flush=True)
_,services=a.call('/services');service=next(s for s in services if s['providerId']==1)
day=datetime.now(ZoneInfo('Europe/Warsaw')).date()+timedelta(days=7)
while day.weekday()!=0:day+=timedelta(days=1)
_,slots=a.call(f'/services/{service["id"]}/available-slots?date={day}')
payload={'serviceId':service['id'],'startTime':slots[0]['startTime'],'customerName':'Identity Test','customerEmail':'identity-customer@example.com','userId':users[1]['id']}
key=str(uuid.uuid4());headers={'Idempotency-Key':key}
with ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda _:a.call('/bookings','POST',payload,headers),range(2)))
assert sorted(s for s,_ in results)==[200,201],[(s,body.get('code') if isinstance(body,dict) else None) for s,body in results]
bid=results[0][1]['id'];assert all(body['id']==bid for _,body in results)
assert a.call('/bookings','POST',{**payload,'customerName':'Changed'},headers)[0]==409
assert a.call('/bookings/'+str(bid))[0]==200
assert b.call('/bookings/'+str(bid))[0]==404
assert b.call('/bookings/'+str(bid)+'/cancel','POST')[0]==404
assert any(item['id']==bid for item in a.call('/bookings')[1])
assert not any(item['id']==bid for item in b.call('/bookings')[1])
assert a.call('/bookings/'+str(bid)+'/cancel','POST',csrf=False)[0]==400
assert b.call('/bookings','POST',payload,headers)[0]==409
print('Forged userId ignored; concurrent replay safe; other account cannot read/cancel/list/replay booking',flush=True)
payload2={**payload,'startTime':slots[4]['startTime']}
status,booking2=b.call('/bookings','POST',payload2,headers);assert status==201,('second account booking',status)
assert a.call('/bookings/'+str(booking2['id']))[0]==404
print('Idempotency key scoped to account: same key creates a separate own booking',flush=True)
assert a.call('/bookings/'+str(bid)+'/cancel','POST')[0]==204
assert b.call('/bookings/'+str(booking2['id'])+'/cancel','POST')[0]==204
race_payload={**payload,'startTime':slots[8]['startTime']}
for attempt in range(5):
 with ThreadPoolExecutor(max_workers=2) as pool:
  race_results=list(pool.map(lambda client:client.call('/bookings','POST',race_payload,{'Idempotency-Key':str(uuid.uuid4())}),clients))
 assert sorted(status for status,_ in race_results)==[201,409],[(status,body.get('code') if isinstance(body,dict) else None) for status,body in race_results]
 for client,(status,body) in zip(clients,race_results):
  if status==201:assert client.call('/bookings/'+str(body['id'])+'/cancel','POST')[0]==204
print('Five independent booking races: each returned one 201 and one 409, without server errors',flush=True)
assert a.call('/auth/logout','POST')[0]==204
assert a.call('/bookings')[0]==401
print('Owner cancellations: 204; logout removes private access; test bookings cancelled',flush=True)

// Host-only fake OpenXR runtime: validates interception and recording, not sensor cadence.
#include "gaze_timestamp.cpp"
#include <condition_variable>
#include <iostream>
#include <stdexcept>
#include <thread>
#include <vector>
#include <functional>
#include <cstddef>
static_assert(sizeof(GazeRecord)==136, "Managed record ABI changed");
static_assert(sizeof(GazeStatus)==56, "Managed status ABI changed");
static_assert(offsetof(GazeRecord,qx)==80, "Managed pose offset changed");

#define CHECK(x) do { if(!(x)) throw std::runtime_error(std::string(__func__)+":"+std::to_string(__LINE__)+" " #x); } while(false)
namespace test {
template<class T> T handle(uintptr_t n) { return reinterpret_cast<T>(n); }
const auto instance=handle<XrInstance>(1);
const auto session=handle<XrSession>(2);
const auto actionSet=handle<XrActionSet>(3);
const auto base=handle<XrSpace>(4);
std::atomic<int> result{XR_SUCCESS};
std::atomic<int64_t> poseTime{900};
std::atomic<bool> writeTime{true};
std::atomic<uintptr_t> nextAction{10},nextSpace{100};
std::atomic<int> timestampCount{0};
std::atomic<XrSpaceLocationFlags> flags{3};
std::atomic<bool> initialTimeWasZero{true};
std::mutex blockingGate;
std::condition_variable blockingCv;
bool blockLocate=false, enteredLocate=false, releaseLocate=false;
void* expectedTail=nullptr;
std::atomic<bool> tailSeen{false};
XrResult XRAPI_CALL stringToPath(XrInstance,const char* text,XrPath* path) {
    // Querying status inside downstream functions also checks the adapter holds no mutex here.
    GazeStatus s{}; ng_status(&s);
    *path=std::strcmp(text,"/interaction_profiles/ext/eye_gaze_interaction")==0?11:12;
    return XR_SUCCESS;
}
XrResult XRAPI_CALL mockSuggest(XrInstance,const XrInteractionProfileSuggestedBinding*) { return static_cast<XrResult>(result.load()); }
XrResult XRAPI_CALL mockAction(XrActionSet,const XrActionCreateInfo*,XrAction* out) { *out=handle<XrAction>(nextAction++); return XR_SUCCESS; }
XrResult XRAPI_CALL mockSpace(XrSession,const XrActionSpaceCreateInfo*,XrSpace* out) { *out=handle<XrSpace>(nextSpace++); return XR_SUCCESS; }
XrResult XRAPI_CALL mockLocate(XrSpace,XrSpace,XrTime,XrSpaceLocation* location) {
    GazeStatus s{}; ng_status(&s);
    { std::unique_lock<std::mutex> lock(blockingGate);
      if(blockLocate) { enteredLocate=true;blockingCv.notify_all();blockingCv.wait(lock,[]{return releaseLocate;}); }
    }
    int n=0;
    if(location) {
        auto node=static_cast<XrBaseOutStructure*>(location->next);
        for(int guard=0;node && guard<128;++guard,node=node->next) {
            if(node==expectedTail) tailSeen=true;
            if(node->type==XR_TYPE_EYE_GAZE_SAMPLE_TIME_EXT) {
                ++n; auto t=reinterpret_cast<XrEyeGazeSampleTimeEXT*>(node);
                if(t->time!=0) initialTimeWasZero=false;
                if(writeTime) t->time=poseTime;
            }
        }
        location->locationFlags=flags;
        location->pose={{0,0,0,1},{1,2,3}};
    }
    timestampCount=n;
    return static_cast<XrResult>(result.load());
}
XrResult XRAPI_CALL mockDestroySpace(XrSpace) { return static_cast<XrResult>(result.load()); }
XrResult XRAPI_CALL mockDestroyAction(XrAction) { return static_cast<XrResult>(result.load()); }
XrResult XRAPI_CALL mockDestroySet(XrActionSet) { return static_cast<XrResult>(result.load()); }
XrResult XRAPI_CALL mockDestroySession(XrSession) { return static_cast<XrResult>(result.load()); }
XrResult XRAPI_CALL mockGet(XrInstance,const char* name,PFN_xrVoidFunction* fn) {
#define MAP(api,mock) if(std::strcmp(name,#api)==0) {*fn=reinterpret_cast<PFN_xrVoidFunction>(mock);return XR_SUCCESS;}
    MAP(xrStringToPath,stringToPath)
    MAP(xrSuggestInteractionProfileBindings,mockSuggest) MAP(xrCreateAction,mockAction)
    MAP(xrCreateActionSpace,mockSpace) MAP(xrLocateSpace,mockLocate)
    MAP(xrDestroySpace,mockDestroySpace) MAP(xrDestroyAction,mockDestroyAction)
    MAP(xrDestroyActionSet,mockDestroySet) MAP(xrDestroySession,mockDestroySession)
#undef MAP
    *fn=nullptr;return XR_ERROR_FUNCTION_UNSUPPORTED;
}
void reset() {
    ng_end();result=XR_SUCCESS;poseTime=900;writeTime=true;nextAction=10;nextSpace=100;
    expectedTail=nullptr;tailSeen=false;initialTimeWasZero=true;timestampCount=0;flags=3;
    {std::lock_guard<std::mutex> lock(blockingGate);blockLocate=enteredLocate=releaseLocate=false;}
    auto hooked=ng_hook(mockGet);
    const char* names[]={"xrSuggestInteractionProfileBindings","xrCreateAction","xrCreateActionSpace","xrLocateSpace","xrDestroySpace","xrDestroyAction","xrDestroyActionSet","xrDestroySession"};
    for(auto name:names) {PFN_xrVoidFunction fn=nullptr;CHECK(hooked(instance,name,&fn)==XR_SUCCESS);CHECK(fn!=nullptr);}
    PFN_xrVoidFunction unknown=reinterpret_cast<PFN_xrVoidFunction>(mockLocate);
    CHECK(hooked(instance,"xrUnrecognizedDiagnosticTest",&unknown)==XR_ERROR_FUNCTION_UNSUPPORTED);CHECK(unknown==nullptr);
    CHECK(ng_begin(id(instance),1)==1);
}
XrAction makeAction(XrActionSet set=actionSet) {
    XrActionCreateInfo info{};info.type=XR_TYPE_ACTION_CREATE_INFO; XrAction out{};
    CHECK(createAction(set,&info,&out)==XR_SUCCESS);return out;
}
XrSpace makeSpace(XrAction action,XrSession sess=session) {
    XrActionSpaceCreateInfo info{};info.type=XR_TYPE_ACTION_SPACE_CREATE_INFO;info.action=action;
    info.poseInActionSpace={{0,0,0,1},{.1f,.2f,.3f}};XrSpace out{};
    CHECK(createSpace(sess,&info,&out)==XR_SUCCESS);return out;
}
void bind(std::initializer_list<XrAction> list,XrPath profile=11) {
    std::vector<XrActionSuggestedBinding> bindings;
    for(auto a:list) bindings.push_back({a,12});
    XrInteractionProfileSuggestedBinding info{};info.type=XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING;
    info.interactionProfile=profile;info.countSuggestedBindings=static_cast<uint32_t>(bindings.size());info.suggestedBindings=bindings.data();
    CHECK(suggest(instance,&info)==result.load());
}
XrResult query(XrSpace space,XrTime requested=1000,void* chain=nullptr) {
    XrSpaceLocation location{};location.type=XR_TYPE_SPACE_LOCATION;location.next=chain;
    auto r=locate(space,base,requested,&location);CHECK(location.next==chain);return r;
}
std::vector<GazeRecord> drain() {std::vector<GazeRecord> out(capacity);int n=ng_drain(out.data(),static_cast<int>(out.size()));out.resize(n);return out;}
GazeStatus status() {GazeStatus s{};ng_status(&s);return s;}
void bindingOrders() {
    reset();auto a=makeAction();auto sp=makeSpace(a);query(sp);CHECK(drain().empty());
    bind({a});query(sp);CHECK(drain().size()==1);
    reset();a=makeAction();bind({a});sp=makeSpace(a);auto sp2=makeSpace(a);query(sp);query(sp2);
    auto r=drain();CHECK(r.size()==2);CHECK(r[0].space!=r[1].space);CHECK(r[0].offsetY==.2f);
    auto b=makeAction();auto other=makeSpace(b);bind({b});query(sp);query(other);CHECK(drain().size()==1);
    bind({a},77);query(other);CHECK(drain().size()==1); // Other profile cannot replace eye bindings.
    result=XR_ERROR_RUNTIME_FAILURE;bind({a});result=XR_SUCCESS;query(other);CHECK(drain().size()==1);
    bind({a,b});query(sp);query(other);CHECK(drain().size()==2);
}
void outputChainsAndResults() {
    reset();auto a=makeAction();bind({a});auto sp=makeSpace(a);
    XrBaseOutStructure tail{};tail.type=XR_TYPE_SPACE_VELOCITY;expectedTail=&tail;
    query(sp,1000,&tail);CHECK(tailSeen);CHECK(timestampCount==1);CHECK(tail.next==nullptr);
    XrEyeGazeSampleTimeEXT time{XR_TYPE_EYE_GAZE_SAMPLE_TIME_EXT,&tail,555};tailSeen=false;
    query(sp,1000,&time);CHECK(tailSeen);CHECK(timestampCount==1);CHECK(initialTimeWasZero);CHECK(time.time==900);CHECK(time.next==&tail);
    writeTime=false;time.time=999;query(sp,1000,&time);auto records=drain();CHECK(records.size()==3);CHECK(records.back().runtimePoseTime==0);
    writeTime=true;result=XR_TIMEOUT_EXPIRED;CHECK(query(sp)==XR_TIMEOUT_EXPIRED);records=drain();CHECK(records[0].runtimePoseTime==900);CHECK(records[0].py==2);
    flags=0;query(sp);records=drain();CHECK(records[0].locationFlags==0);CHECK(records[0].runtimePoseTime==900);
    flags=15;query(sp);records=drain();CHECK(records[0].locationFlags==15);
    result=XR_ERROR_RUNTIME_FAILURE;CHECK(query(sp)==XR_ERROR_RUNTIME_FAILURE);records=drain();CHECK(records[0].runtimePoseTime==0);CHECK(records[0].locationFlags==0);CHECK(records[0].py==0);CHECK(records[0].result==XR_ERROR_RUNTIME_FAILURE);
}
void timestampsAndOverflow() {
    reset();auto a=makeAction();bind({a});auto sp=makeSpace(a);
    for(auto t:{0,900,900,800,1500}) {poseTime=t;query(sp);}
    auto r=drain();CHECK(r.size()==5);CHECK(r[0].runtimePoseTime==0);CHECK(r[1].runtimePoseTime==r[2].runtimePoseTime);CHECK(r[3].runtimePoseTime==800);CHECK(r[4].runtimePoseTime>r[4].requestedTime);
    for(size_t i=0;i<capacity+19;++i) query(sp);
    auto s=status();CHECK(s.dropped==19);CHECK(s.records==capacity+24);r=drain();CHECK(r.size()==capacity);CHECK(r.front().sequence==25);CHECK(r.back().sequence==s.records);
    CHECK(ng_drain(nullptr,10)==0);GazeRecord record{};CHECK(ng_drain(&record,0)==0);CHECK(ng_drain(&record,-1)==0);
}
void lifecycleAndReuse() {
    reset();auto a=makeAction();bind({a});auto sp=makeSpace(a);
    result=XR_ERROR_RUNTIME_FAILURE;destroySpace(sp);result=XR_SUCCESS;query(sp);CHECK(drain().size()==1);
    destroySpace(sp);query(sp);CHECK(drain().empty());nextSpace=id(sp);auto reused=makeSpace(a);CHECK(reused==sp);query(reused);CHECK(drain().size()==1);
    destroyAction(a);query(sp);CHECK(drain().empty());nextAction=id(a);a=makeAction();sp=makeSpace(a);query(sp);CHECK(drain().empty());bind({a});query(sp);CHECK(drain().size()==1);
    auto otherSet=handle<XrActionSet>(99);auto b=makeAction(otherSet);auto bp=makeSpace(b);bind({a,b});destroySet(actionSet);query(sp);query(bp);CHECK(drain().size()==1);
    auto otherSession=handle<XrSession>(88);auto b2=makeSpace(b,otherSession);destroySession(session);query(bp);query(b2);CHECK(drain().size()==1);
    query(b2);ng_end();CHECK(!status().enabled);CHECK(drain().size()==1);query(b2);CHECK(drain().empty());
    CHECK(ng_begin(id(instance),0)==0);CHECK(!status().enabled);query(b2);CHECK(timestampCount==0);CHECK(drain().empty());
}
void concurrentDrain() {
    reset();auto a=makeAction();bind({a});auto sp=makeSpace(a);std::atomic<bool> done{false};std::vector<uint64_t> sequences;
    std::thread producer([&]{for(int i=0;i<5000;++i) query(sp);done=true;});
    while(!done) {for(auto& r:drain()) sequences.push_back(r.sequence);std::this_thread::yield();}
    producer.join();for(auto& r:drain())sequences.push_back(r.sequence);
    CHECK(sequences.size()==5000);CHECK(status().dropped==0);for(size_t i=0;i<sequences.size();++i) CHECK(sequences[i]==i+1);
}
void restartWhileInFlight() {
    reset();auto a=makeAction();bind({a});auto sp=makeSpace(a);auto oldEpoch=status().epoch;
    {std::lock_guard<std::mutex> lock(blockingGate);blockLocate=true;}
    std::thread producer([&]{query(sp);});
    {std::unique_lock<std::mutex> lock(blockingGate);blockingCv.wait(lock,[]{return enteredLocate;});}
    ng_end();CHECK(ng_begin(id(instance),1)==1);
    {std::lock_guard<std::mutex> lock(blockingGate);releaseLocate=true;blockingCv.notify_all();}
    producer.join();CHECK(status().epoch==oldEpoch+1);CHECK(drain().empty());
    a=makeAction();bind({a});sp=makeSpace(a);query(sp);auto r=drain();CHECK(r.size()==1);CHECK(r[0].sequence==1);CHECK(r[0].epoch==oldEpoch+1);
}
}
int main() {
    const std::pair<const char*,std::function<void()>> tests[]={
        {"binding orders and replacement",test::bindingOrders},{"output chain and result preservation",test::outputChainsAndResults},
        {"timestamp fidelity and bounded overflow",test::timestampsAndOverflow},{"lifecycle and handle reuse",test::lifecycleAndReuse},
        {"concurrent drain and locate",test::concurrentDrain},{"restart discards in-flight old epoch",test::restartWhileInFlight}};
    try {for(const auto& item:tests) {item.second();std::cout<<"PASS "<<item.first<<'\n';}}
    catch(const std::exception& e) {std::cerr<<"FAIL "<<e.what()<<'\n';return 1;}
    std::cout<<"6 native interception suites passed; these do not establish sensor timing or headset compatibility.\n";
}

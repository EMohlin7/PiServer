
async function CreateAccount(event)
{
    event.preventDefault();
    PopUp("Creating account...");
    const form = event.currentTarget;
    const formData = new FormData(form);
    const data = Object.fromEntries(formData.entries());
    
    if(data["password"] != data["confirmPassword"])
    {
        PopUp("The passwords have to match");
        return;
    }
    const res = await fetch(host + "/newaccount", {
        method: "post",
        body: JSON.stringify(data),
        headers: {"Content-Type":"application/json"}
    });
    const object = await res.json();
    
    PopUp(object['message']);
    if(object['status'] == 200)
    {
        localStorage.setItem("accessToken", object['accessToken']);
        window.location.assign("/");
    }
}


document.getElementById("accountForm").addEventListener("submit", CreateAccount);
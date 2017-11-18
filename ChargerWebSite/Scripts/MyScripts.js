function checkEmptyText(field, required = false)
{
    if (required == true) {
        if (field.value == "") {
            field.style.border = "1px solid red";
            return false;
        }
        else {
            field.style.border = "";
            return true;
        }
    } else {
        if (field.value == "") {
            field.style.border = "1px solid blue";
            return false;
        }
        else {
            field.style.border = "";
            return true;
        }
    }
}

function ValidSignUp(form)
{   
    var email = document.getElementById("email");
    var password = document.getElementById("password");
    var password2 = document.getElementById("password2");
    var errorMessage = document.getElementById("ErrorMessage");
    var firstname = document.getElementById("firstName");
    var lastname = document.getElementById("lastName");
    var organizationname = document.getElementById("organizationName");
    var phonenumber = document.getElementById("phoneNumber");
    var selected = document.getElementById("customerType").value;
    var serverMessage = document.getElementById("ServerMessage");


    if (!checkEmptyText(email, true))
    {
        try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
        
        errorMessage.innerHTML = "Введите Email";
        errorMessage.style.display = "block";
        errorMessage.style.textAlign = "center";
        errorMessage.style.border = "1px solid red";
        email.focus();
        return false;
    }
    else if (!checkEmptyText(password, true))
    {
        try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
        errorMessage.innerHTML = "Введите пароль";
        errorMessage.style.display = "block";
        errorMessage.style.textAlign = "center";
        errorMessage.style.border = "1px solid red";
        password.focus();
        return false;
    }
    else if (!checkEmptyText(password2, true) || password.value != password2.value) {
        try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
        errorMessage.innerHTML = "Пароли не совпадают";
        errorMessage.style.display = "block";
        errorMessage.style.textAlign = "center";
        errorMessage.style.border = "1px solid red";
        password2.focus();
        return false;
    }
    else if (selected == 1 && !checkEmptyText(firstname, true))
    {
        try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
            errorMessage.innerHTML = "Введите Имя";
            errorMessage.style.display = "block";
            errorMessage.style.textAlign = "center";
            errorMessage.style.border = "1px solid red";
            firstname.focus();
            return false;
    }
    else if (selected == 1 && !checkEmptyText(lastname, true)) {
        try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
            errorMessage.innerHTML = "Введите Фамилию";
            errorMessage.style.display = "block";
            errorMessage.style.textAlign = "center";
            errorMessage.style.border = "1px solid red";
            lastname.focus();
            return false;
        }
 
    else if (selected == 2 && !checkEmptyText(organizationname, true))
    {
        try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
            errorMessage.innerHTML = "Введите название организации";
            errorMessage.style.display = "block";
            errorMessage.style.textAlign = "center";
            errorMessage.style.border = "1px solid red";
            organizationname.focus();
            return false; 
    }
    else if (!checkEmptyText(phonenumber, true)) {
         try {
            serverMessage.style.display = "none";
        } catch (ex)
        { }
        errorMessage.innerHTML = "Введите телефонный номер";
        errorMessage.style.display = "block";
        errorMessage.style.textAlign = "center";
        errorMessage.style.border = "1px solid red";
        phonenumber.focus();
        return false;
    }
    else {
        errorMessage.style.display = "none";
        return true;
        form.submit();
    }
}

function Unblock() {

    var firstname = document.getElementById("firstName");
    var lastname = document.getElementById("lastName");
    var organizationname = document.getElementById("organizationName");

    var selected = document.getElementById("customerType").value;
    if (selected == null) return;
    if (selected == 1) {
       
        var errorMessage = document.getElementById("ErrorMessage");
        if (errorMessage.style.display == "block") {
            errorMessage.style.display = "none";
        }

        var organization = document.getElementById("Organization");
        organization.style.display = "none";

        var customer = document.getElementById("SimpleCustomer");
        customer.style.display = "block";
        document.getElementById("firstName").style.border = "";
        document.getElementById("lastName").style.border = "";
        document.getElementById("organizationName").innerHTML = "";
    }
    else {
        var errorMessage = document.getElementById("ErrorMessage");
        if (errorMessage.style.display == "block") {
            errorMessage.style.display = "none";
        }

        var customer = document.getElementById("SimpleCustomer");
        customer.style.display = "none";

        var organization = document.getElementById("Organization");
        organization.style.display = "block";
        organizationname.style.border = "";
        document.getElementById("firstName").innerHTML = "";
        document.getElementById("lastName").innerHTML = "";
    }
}


